using Microsoft.EntityFrameworkCore;
using Tugu.Application.Common.Exceptions;
using Tugu.Application.Transactions;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;
using Tugu.Infrastructure.Persistence;
using Tugu.Infrastructure.Transactions;

namespace Tugu.Tests.Transactions;

/// <summary>
/// Tests de integración del motor transaccional contra el PostgreSQL local de
/// docker-compose. La atomicidad, el lock de fila y el índice único de
/// idempotencia son comportamiento de la base real: probarlos contra memoria
/// no probaría nada. Si Postgres no está corriendo, los tests se marcan como
/// omitidos (Skipped) en lugar de fallar.
/// </summary>
public class TransactionEngineTests : IAsyncLifetime
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
            if (_dbAvailable)
                await db.Database.MigrateAsync();
        }
        catch
        {
            _dbAvailable = false;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private void RequireDb() =>
        Skip.IfNot(_dbAvailable, "PostgreSQL local no disponible (docker compose up -d).");

    /// <summary>Crea usuario + billetera únicos para aislar cada test.</summary>
    private static async Task<Wallet> SeedWalletAsync(decimal balance, UserStatus userStatus = UserStatus.Active)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        await using var db = NewContext();

        var user = new User
        {
            DocumentType = DocumentType.CC,
            DocumentNumber = $"T{suffix}",
            FirstName = "Test",
            LastName = "Engine",
            PhoneNumber = $"T{suffix[..10]}",
            Status = userStatus,
            CreatedBy = "test"
        };
        var wallet = new Wallet { UserId = user.Id, Balance = balance, CreatedBy = "test" };

        db.Users.Add(user);
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();
        return wallet;
    }

    private static async Task<decimal> GetBalanceAsync(Guid walletId)
    {
        await using var db = NewContext();
        return (await db.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletId)).Balance;
    }

    private static TransactionCommand Recharge(Guid walletId, decimal amount, Guid key) =>
        new(walletId, TransactionType.Recharge, amount, key, null, null, "test");

    private static TransactionCommand Withdraw(Guid walletId, decimal amount, Guid key) =>
        new(walletId, TransactionType.Withdrawal, amount, key, null, null, "test");

    [SkippableFact]
    public async Task Recarga_ActualizaSaldoYGuardaBalanceAfter()
    {
        RequireDb();
        var wallet = await SeedWalletAsync(10_000m);

        await using var db = NewContext();
        var result = await new TransactionEngine(db).ExecuteAsync(
            Recharge(wallet.Id, 5_000m, Guid.NewGuid()));

        Assert.False(result.WasReplay);
        Assert.Equal(TransactionStatus.Completed, result.Transaction.Status);
        Assert.Equal(15_000m, result.Transaction.BalanceAfter);
        Assert.Equal(15_000m, await GetBalanceAsync(wallet.Id));
    }

    [SkippableFact]
    public async Task DoblePeticionMismaKey_Secuencial_NoDuplicaElSaldo()
    {
        RequireDb();
        var wallet = await SeedWalletAsync(0m);
        var key = Guid.NewGuid();

        await using var db1 = NewContext();
        var first = await new TransactionEngine(db1).ExecuteAsync(Recharge(wallet.Id, 20_000m, key));

        await using var db2 = NewContext();
        var second = await new TransactionEngine(db2).ExecuteAsync(Recharge(wallet.Id, 20_000m, key));

        Assert.False(first.WasReplay);
        Assert.True(second.WasReplay);
        Assert.Equal(first.Transaction.Id, second.Transaction.Id);
        Assert.Equal(20_000m, await GetBalanceAsync(wallet.Id)); // una sola vez
    }

    [SkippableFact]
    public async Task DoblePeticionMismaKey_Simultanea_NoDuplicaElSaldo()
    {
        RequireDb();
        var wallet = await SeedWalletAsync(0m);
        var key = Guid.NewGuid();

        async Task<TransactionResult> Run()
        {
            await using var db = NewContext();
            return await new TransactionEngine(db).ExecuteAsync(Recharge(wallet.Id, 20_000m, key));
        }

        var results = await Task.WhenAll(Run(), Run());

        Assert.Single(results.Select(r => r.Transaction.Id).Distinct());
        Assert.Equal(1, results.Count(r => !r.WasReplay));
        Assert.Equal(20_000m, await GetBalanceAsync(wallet.Id)); // una sola vez
    }

    [SkippableFact]
    public async Task Retiro_SaldoInsuficiente_LanzaYNoTocaElSaldo()
    {
        RequireDb();
        var wallet = await SeedWalletAsync(10_000m);

        await using var db = NewContext();
        await Assert.ThrowsAsync<InsufficientFundsException>(() =>
            new TransactionEngine(db).ExecuteAsync(Withdraw(wallet.Id, 50_000m, Guid.NewGuid())));

        Assert.Equal(10_000m, await GetBalanceAsync(wallet.Id));
    }

    [SkippableFact]
    public async Task DosRetirosSimultaneos_SoloUnoPasaSiElSaldoAlcanzaParaUno()
    {
        RequireDb();
        var wallet = await SeedWalletAsync(100_000m);

        async Task<Exception?> Run()
        {
            try
            {
                await using var db = NewContext();
                await new TransactionEngine(db).ExecuteAsync(
                    Withdraw(wallet.Id, 80_000m, Guid.NewGuid()));
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        var outcomes = await Task.WhenAll(Run(), Run());

        Assert.Equal(1, outcomes.Count(o => o is null));                         // uno pasó
        Assert.Equal(1, outcomes.Count(o => o is InsufficientFundsException));   // el otro no
        Assert.Equal(20_000m, await GetBalanceAsync(wallet.Id));                 // 100k - 80k
    }

    [SkippableFact]
    public async Task UsuarioBloqueado_NoPuedeRecargar()
    {
        RequireDb();
        var wallet = await SeedWalletAsync(10_000m, UserStatus.Blocked);

        await using var db = NewContext();
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            new TransactionEngine(db).ExecuteAsync(Recharge(wallet.Id, 5_000m, Guid.NewGuid())));

        Assert.Contains("bloqueado", ex.Message);
        Assert.Equal(10_000m, await GetBalanceAsync(wallet.Id));
    }

    [SkippableFact]
    public async Task MismaKeyConParametrosDistintos_SeRechaza()
    {
        RequireDb();
        var wallet = await SeedWalletAsync(0m);
        var key = Guid.NewGuid();

        await using var db1 = NewContext();
        await new TransactionEngine(db1).ExecuteAsync(Recharge(wallet.Id, 20_000m, key));

        await using var db2 = NewContext();
        await Assert.ThrowsAsync<ConflictException>(() =>
            new TransactionEngine(db2).ExecuteAsync(Recharge(wallet.Id, 99_000m, key)));
    }
}
