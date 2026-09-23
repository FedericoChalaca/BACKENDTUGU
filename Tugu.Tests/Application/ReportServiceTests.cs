using Tugu.Application.Common.Exceptions;
using Tugu.Application.Common.Interfaces;
using Tugu.Application.Reports;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;
using Tugu.Infrastructure.Persistence.InMemory;

namespace Tugu.Tests.Application;

public class ReportServiceTests
{
    private readonly InMemoryTransactionRepository _transactions = new();
    private readonly InMemoryWalletRepository _wallets = new();
    private readonly InMemoryCompanyRepository _companies = new();
    private readonly InMemoryDeviceRepository _devices = new();
    private readonly ReportService _service;

    private readonly Wallet _userWallet;
    private readonly Company _company;
    private readonly Wallet _companyWallet;
    private readonly Device _device;
    private static readonly DateTime Day0 = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    public ReportServiceTests()
    {
        _service = new ReportService(_transactions, _wallets, _companies, _devices);

        _userWallet = new Wallet { UserId = Guid.NewGuid(), OwnerType = WalletOwnerType.User };
        _company = new Company { Name = "Tienda", Nit = "900000000-1" };
        _companyWallet = new Wallet { CompanyId = _company.Id, OwnerType = WalletOwnerType.Company, Company = _company };
        _device = new Device { SerialNumber = "SN-R", Alias = "d", CompanyId = _company.Id };

        _wallets.AddAsync(_userWallet).GetAwaiter().GetResult();
        _wallets.AddAsync(_companyWallet).GetAwaiter().GetResult();
        _companies.AddAsync(_company).GetAwaiter().GetResult();
        _devices.AddAsync(_device).GetAwaiter().GetResult();

        _transactions.Seed(
            Tx(_userWallet, Day0, TransactionType.Recharge, 10_000m, TransactionStatus.Completed),
            Tx(_userWallet, Day0.AddDays(1), TransactionType.Withdrawal, 4_000m, TransactionStatus.Completed, _device.Id),
            Tx(_userWallet, Day0.AddDays(2), TransactionType.Recharge, 99_999m, TransactionStatus.Failed),     // no suma
            Tx(_companyWallet, Day0.AddDays(3), TransactionType.Recharge, 50_000m, TransactionStatus.Completed, _device.Id),
            Tx(_companyWallet, Day0.AddDays(4), TransactionType.Withdrawal, 20_000m, TransactionStatus.Completed, _device.Id));
    }

    private static Transaction Tx(Wallet w, DateTime at, TransactionType type, decimal amount, TransactionStatus status, Guid? deviceId = null) =>
        new() { WalletId = w.Id, Wallet = w, Type = type, Amount = amount, Status = status, DeviceId = deviceId, IdempotencyKey = Guid.NewGuid(), CreatedAt = at };

    private static ReportFilter F(Guid? wallet = null, Guid? company = null, Guid? device = null,
        TransactionType? type = null, TransactionStatus? status = null, DateTime? from = null, DateTime? to = null) =>
        new(wallet, company, device, type, status, from, to);

    [Fact]
    public async Task Summary_Billetera_SoloSumaCompletadas()
    {
        var s = await _service.SummaryAsync(F(wallet: _userWallet.Id));

        Assert.Equal(3, s.Count);
        Assert.Equal(10_000m, s.TotalIn);    // la Failed de 99.999 no cuenta
        Assert.Equal(4_000m, s.TotalOut);
        Assert.Equal(6_000m, s.Net);
        Assert.Equal(2, s.ByStatus[TransactionStatus.Completed]);
        Assert.Equal(1, s.ByStatus[TransactionStatus.Failed]);
    }

    [Fact]
    public async Task Summary_Comercio_AgrupaSuBilletera()
    {
        var s = await _service.SummaryAsync(F(company: _company.Id));

        Assert.Equal(2, s.Count);
        Assert.Equal(50_000m, s.TotalIn);
        Assert.Equal(20_000m, s.TotalOut);
    }

    [Fact]
    public async Task Summary_Corresponsal_CruzaBilleterasDeUsuarioYComercio()
    {
        var s = await _service.SummaryAsync(F(device: _device.Id));

        Assert.Equal(3, s.Count);
        Assert.Equal(50_000m, s.TotalIn);
        Assert.Equal(24_000m, s.TotalOut);   // 4.000 del usuario + 20.000 del comercio
    }

    [Fact]
    public async Task Summary_FiltroPorFecha_Inclusivo()
    {
        var s = await _service.SummaryAsync(F(wallet: _userWallet.Id, from: Day0.AddDays(1), to: Day0.AddDays(1)));

        Assert.Equal(1, s.Count);
        Assert.Equal(4_000m, s.TotalOut);
    }

    [Fact]
    public async Task Transactions_PaginaMasRecientePrimero()
    {
        var page = await _service.TransactionsAsync(F(device: _device.Id), page: 1, pageSize: 2);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(20_000m, page.Items[0].Amount);
    }

    [Fact]
    public async Task SinNingunFiltroDeAlcance_LanzaValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.SummaryAsync(F()));
    }

    [Fact]
    public async Task RangoMayorAUnAnio_LanzaValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SummaryAsync(F(wallet: _userWallet.Id, from: Day0, to: Day0.AddDays(400))));
    }

    [Fact]
    public async Task ComercioInexistente_LanzaNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.SummaryAsync(F(company: Guid.NewGuid())));
    }

    [Fact]
    public async Task PageSizeFueraDeRango_LanzaValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.TransactionsAsync(F(wallet: _userWallet.Id), 1, ReportService.MaxPageSize + 1));
    }
}
