using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tugu.Application.Common.Exceptions;
using Tugu.Application.Transactions;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;
using Tugu.Infrastructure.Persistence;
using Tugu.Infrastructure.Transactions;

namespace Tugu.Tests.Transactions;

/// <summary>
/// [P1][QA] Pruebas financieras: billetera bloqueada, timeout a mitad de la
/// operación y rollback real de la base. Contra PostgreSQL real; se omiten sin él.
/// </summary>
public class TransactionEngineResilienceTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=tugu;Username=tugu;Password=tugu_dev;Timeout=3";

    private bool _dbAvailable;

    private static TuguDbContext NewContext() => new(
        new DbContextOptionsBuilder<TuguDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        try
        {
            await using var db = NewContext();
            _dbAvailable = await db.Database.CanConnectAsync();
            if (_dbAvailable) await TestDatabase.EnsureMigratedAsync(db);
        }
        catch
        {
            _dbAvailable = false;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private void RequireDb() =>
        Skip.IfNot(_dbAvailable, "PostgreSQL local no disponible (docker compose up -d).");

    private static async Task<Wallet> SeedWalletAsync(decimal balance, WalletStatus status = WalletStatus.Active)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        await using var db = NewContext();

        var user = new User
        {
            DocumentType = DocumentType.CC, DocumentNumber = $"R{suffix}",
            FirstName = "Test", LastName = "Resilience", PhoneNumber = $"R{suffix[..10]}",
            Status = UserStatus.Active, CreatedBy = "test"
        };
        var wallet = new Wallet { UserId = user.Id, Balance = balance, Status = status, CreatedBy = "test" };

        db.Users.Add(user);
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();
        return wallet;
    }

    private static async Task<(decimal Balance, int TxWithKey)> SnapshotAsync(Guid walletId, Guid key)
    {
        await using var db = NewContext();
        var balance = (await db.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletId)).Balance;
        var count = await db.Transactions.CountAsync(t => t.IdempotencyKey == key);
        return (balance, count);
    }

    private static TransactionCommand Recharge(Guid walletId, decimal amount, Guid key, string createdBy = "test") =>
        new(walletId, TransactionType.Recharge, amount, key, null, null, createdBy);

    [SkippableFact]
    public async Task BilleteraCongelada_NoMueveSaldo()
    {
        RequireDb();
        var wallet = await SeedWalletAsync(10_000m, WalletStatus.Frozen);
        var key = Guid.NewGuid();

        await using var db = NewContext();
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            new TransactionEngine(db).ExecuteAsync(Recharge(wallet.Id, 5_000m, key)));

        Assert.Contains("no está activa", ex.Message);
        Assert.Equal((10_000m, 0), await SnapshotAsync(wallet.Id, key));
    }

    /// <summary>
    /// Timeout: otra operación tiene la billetera bloqueada; la petición se
    /// cancela esperando el lock. No debe quedar NADA guardado, y el reintento
    /// del cliente con la MISMA clave debe aplicar la operación una sola vez.
    /// </summary>
    [SkippableFact]
    public async Task Timeout_NoDejaNadaYElReintentoConLaMismaClaveAplicaUnaVez()
    {
        RequireDb();
        var wallet = await SeedWalletAsync(10_000m);
        var key = Guid.NewGuid();

        await using (var locker = new NpgsqlConnection(ConnectionString))
        {
            await locker.OpenAsync();
            await using var lockTx = await locker.BeginTransactionAsync();
            await using (var cmd = new NpgsqlCommand(
                "SELECT 1 FROM wallets WHERE \"Id\" = @id FOR UPDATE", locker, lockTx))
            {
                cmd.Parameters.AddWithValue("id", wallet.Id);
                await cmd.ExecuteScalarAsync();
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            await using var db = NewContext();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new TransactionEngine(db).ExecuteAsync(Recharge(wallet.Id, 5_000m, key), cts.Token));

            await lockTx.RollbackAsync();
        }

        Assert.Equal((10_000m, 0), await SnapshotAsync(wallet.Id, key));

        await using var retryDb = NewContext();
        var retry = await new TransactionEngine(retryDb).ExecuteAsync(Recharge(wallet.Id, 5_000m, key));

        Assert.False(retry.WasReplay);
        Assert.Equal((15_000m, 1), await SnapshotAsync(wallet.Id, key));
    }

    /// <summary>
    /// Rollback: la base rechaza el INSERT de la transacción DESPUÉS de que el
    /// motor ya cambió el saldo en la misma transacción SQL. Todo se revierte.
    /// (Se fuerza el fallo con un CreatedBy más largo que su columna varchar(50).)
    /// </summary>
    [SkippableFact]
    public async Task FalloAlGuardar_RevierteSaldoYTransaccion()
    {
        RequireDb();
        var wallet = await SeedWalletAsync(10_000m);
        var key = Guid.NewGuid();

        await using var db = NewContext();
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            new TransactionEngine(db).ExecuteAsync(Recharge(wallet.Id, 5_000m, key, createdBy: new string('x', 60))));

        Assert.Equal((10_000m, 0), await SnapshotAsync(wallet.Id, key));
    }

    [SkippableFact]
    public async Task ReferenciaDemasiadoLarga_Es400YNoTocaSaldo()
    {
        RequireDb();
        var wallet = await SeedWalletAsync(10_000m);
        var key = Guid.NewGuid();

        await using var db = NewContext();
        await Assert.ThrowsAsync<ValidationException>(() => new TransactionEngine(db).ExecuteAsync(
            new TransactionCommand(wallet.Id, TransactionType.Recharge, 1_000m, key, new string('r', 101), null, "test")));

        Assert.Equal((10_000m, 0), await SnapshotAsync(wallet.Id, key));
    }
}
