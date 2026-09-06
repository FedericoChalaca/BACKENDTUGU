using Microsoft.EntityFrameworkCore;
using Tugu.Application.Common.Exceptions;
using Tugu.Application.Transactions;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;
using Tugu.Infrastructure.Persistence;
using Tugu.Infrastructure.Transactions;

namespace Tugu.Tests.Transactions;

/// <summary>
/// Reglas específicas del RETIRO contra PostgreSQL real: corresponsal
/// (datáfono) obligatorio y activo, usuario con KYC verificado, saldo.
/// Se omiten si Postgres local no está disponible.
/// </summary>
public class TransactionEngineWithdrawTests : IAsyncLifetime
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

    private static async Task<(Wallet Wallet, Device Device)> SeedAsync(
        decimal balance, UserStatus userStatus = UserStatus.Active, DeviceStatus deviceStatus = DeviceStatus.Active)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        await using var db = NewContext();

        var user = new User
        {
            DocumentType = DocumentType.CC, DocumentNumber = $"W{suffix}",
            FirstName = "Test", LastName = "Withdraw", PhoneNumber = $"W{suffix[..10]}",
            Status = userStatus, CreatedBy = "test"
        };
        var wallet = new Wallet { UserId = user.Id, Balance = balance, CreatedBy = "test" };
        var device = new Device { SerialNumber = $"SN-{suffix}", Alias = "Test", Status = deviceStatus, CreatedBy = "test" };

        db.Users.Add(user);
        db.Wallets.Add(wallet);
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        return (wallet, device);
    }

    private static async Task<decimal> GetBalanceAsync(Guid walletId)
    {
        await using var db = NewContext();
        return (await db.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletId)).Balance;
    }

    private static TransactionCommand Withdraw(Guid walletId, decimal amount, Guid? deviceId) =>
        new(walletId, TransactionType.Withdrawal, amount, Guid.NewGuid(), "retiro-test", deviceId, "test");

    [SkippableFact]
    public async Task Retiro_Valido_DescuentaSaldoYRegistraDatafono()
    {
        RequireDb();
        var (wallet, device) = await SeedAsync(50_000m);

        await using var db = NewContext();
        var result = await new TransactionEngine(db).ExecuteAsync(Withdraw(wallet.Id, 20_000m, device.Id));

        Assert.Equal(TransactionStatus.Completed, result.Transaction.Status);
        Assert.Equal(30_000m, result.Transaction.BalanceAfter);
        Assert.Equal(device.Id, result.Transaction.DeviceId);
        Assert.Equal(30_000m, await GetBalanceAsync(wallet.Id));
    }

    [SkippableFact]
    public async Task Retiro_SinDatafono_LanzaValidation()
    {
        RequireDb();
        var (wallet, _) = await SeedAsync(50_000m);

        await using var db = NewContext();
        await Assert.ThrowsAsync<ValidationException>(() =>
            new TransactionEngine(db).ExecuteAsync(Withdraw(wallet.Id, 1_000m, deviceId: null)));
    }

    [SkippableFact]
    public async Task Retiro_DatafonoInactivo_LanzaConflictYNoTocaSaldo()
    {
        RequireDb();
        var (wallet, device) = await SeedAsync(50_000m, deviceStatus: DeviceStatus.Inactive);

        await using var db = NewContext();
        await Assert.ThrowsAsync<ConflictException>(() =>
            new TransactionEngine(db).ExecuteAsync(Withdraw(wallet.Id, 1_000m, device.Id)));

        Assert.Equal(50_000m, await GetBalanceAsync(wallet.Id));
    }

    [SkippableFact]
    public async Task Retiro_UsuarioSinVerificar_LanzaConflict()
    {
        RequireDb();
        var (wallet, device) = await SeedAsync(50_000m, userStatus: UserStatus.PendingVerification);

        await using var db = NewContext();
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            new TransactionEngine(db).ExecuteAsync(Withdraw(wallet.Id, 1_000m, device.Id)));

        Assert.Contains("verificada", ex.Message);
        Assert.Equal(50_000m, await GetBalanceAsync(wallet.Id));
    }

    [SkippableFact]
    public async Task Recarga_UsuarioSinVerificar_SiEstaPermitida()
    {
        RequireDb();
        var (wallet, _) = await SeedAsync(0m, userStatus: UserStatus.PendingVerification);

        await using var db = NewContext();
        var result = await new TransactionEngine(db).ExecuteAsync(
            new TransactionCommand(wallet.Id, TransactionType.Recharge, 5_000m, Guid.NewGuid(), null, null, "test"));

        Assert.Equal(5_000m, result.Transaction.BalanceAfter);
    }

    [SkippableFact]
    public async Task Retiro_SaldoInsuficiente_LanzaInsufficientFunds()
    {
        RequireDb();
        var (wallet, device) = await SeedAsync(1_000m);

        await using var db = NewContext();
        await Assert.ThrowsAsync<InsufficientFundsException>(() =>
            new TransactionEngine(db).ExecuteAsync(Withdraw(wallet.Id, 5_000m, device.Id)));
    }
}
