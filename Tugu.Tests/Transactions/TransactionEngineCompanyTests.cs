using Microsoft.EntityFrameworkCore;
using Tugu.Application.Common.Exceptions;
using Tugu.Application.Transactions;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;
using Tugu.Infrastructure.Persistence;
using Tugu.Infrastructure.Transactions;

namespace Tugu.Tests.Transactions;

/// <summary>
/// El motor sobre billeteras de COMERCIO contra PostgreSQL real: recarga,
/// retiro (exige comercio Active) y comercio bloqueado. Se omiten sin Postgres.
/// </summary>
public class TransactionEngineCompanyTests : IAsyncLifetime
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

    private static async Task<(Wallet Wallet, Device Device)> SeedCompanyWalletAsync(
        decimal balance, CompanyStatus status = CompanyStatus.Active)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        await using var db = NewContext();

        var company = new Company
        {
            Name = $"Tienda {suffix}",
            Nit = $"9{suffix[..8].Select(c => (char)('0' + (c % 10))).Aggregate("", (a, c) => a + c)}-1",
            Status = status,
            CreatedBy = "test"
        };
        var wallet = new Wallet
        {
            CompanyId = company.Id, OwnerType = WalletOwnerType.Company, Balance = balance, CreatedBy = "test"
        };
        var device = new Device
        {
            SerialNumber = $"SNC-{suffix}", Alias = "Test", Status = DeviceStatus.Active,
            CompanyId = company.Id, CreatedBy = "test"
        };

        db.Companies.Add(company);
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

    [SkippableFact]
    public async Task RecargaYRetiro_ComercioActivo_FuncionanSobreSuBilletera()
    {
        RequireDb();
        var (wallet, device) = await SeedCompanyWalletAsync(0m);

        await using var db1 = NewContext();
        var recharge = await new TransactionEngine(db1).ExecuteAsync(
            new TransactionCommand(wallet.Id, TransactionType.Recharge, 100_000m, Guid.NewGuid(), null, null, "test"));
        Assert.Equal(100_000m, recharge.Transaction.BalanceAfter);

        await using var db2 = NewContext();
        var withdraw = await new TransactionEngine(db2).ExecuteAsync(
            new TransactionCommand(wallet.Id, TransactionType.Withdrawal, 30_000m, Guid.NewGuid(), null, device.Id, "test"));
        Assert.Equal(70_000m, withdraw.Transaction.BalanceAfter);

        Assert.Equal(70_000m, await GetBalanceAsync(wallet.Id));
    }

    [SkippableFact]
    public async Task Retiro_ComercioSinVerificar_LanzaConflict()
    {
        RequireDb();
        var (wallet, device) = await SeedCompanyWalletAsync(50_000m, CompanyStatus.PendingVerification);

        await using var db = NewContext();
        var ex = await Assert.ThrowsAsync<ConflictException>(() => new TransactionEngine(db).ExecuteAsync(
            new TransactionCommand(wallet.Id, TransactionType.Withdrawal, 1_000m, Guid.NewGuid(), null, device.Id, "test")));

        Assert.Contains("comercio", ex.Message);
        Assert.Equal(50_000m, await GetBalanceAsync(wallet.Id));
    }

    [SkippableFact]
    public async Task Recarga_ComercioBloqueado_LanzaConflict()
    {
        RequireDb();
        var (wallet, _) = await SeedCompanyWalletAsync(0m, CompanyStatus.Blocked);

        await using var db = NewContext();
        await Assert.ThrowsAsync<ConflictException>(() => new TransactionEngine(db).ExecuteAsync(
            new TransactionCommand(wallet.Id, TransactionType.Recharge, 1_000m, Guid.NewGuid(), null, null, "test")));
    }
}
