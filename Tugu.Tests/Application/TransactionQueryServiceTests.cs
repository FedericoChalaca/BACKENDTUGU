using Tugu.Application.Common.Exceptions;
using Tugu.Application.Common.Interfaces;
using Tugu.Application.Transactions;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;
using Tugu.Infrastructure.Persistence.InMemory;

namespace Tugu.Tests.Application;

public class TransactionQueryServiceTests
{
    private readonly InMemoryTransactionRepository _transactions = new();
    private readonly InMemoryWalletRepository _wallets = new();
    private readonly TransactionQueryService _service;
    private readonly Guid _walletId;

    public TransactionQueryServiceTests()
    {
        _service = new TransactionQueryService(_transactions, _wallets);

        var wallet = new Wallet { UserId = Guid.NewGuid(), Balance = 0m };
        _wallets.AddAsync(wallet).GetAwaiter().GetResult();
        _walletId = wallet.Id;

        // 5 movimientos con fechas escalonadas: 3 recargas y 2 retiros.
        var baseDate = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        _transactions.Seed(
            Tx(baseDate.AddDays(0), TransactionType.Recharge, 10_000m, TransactionStatus.Completed),
            Tx(baseDate.AddDays(1), TransactionType.Withdrawal, 2_000m, TransactionStatus.Completed),
            Tx(baseDate.AddDays(2), TransactionType.Recharge, 5_000m, TransactionStatus.Failed),
            Tx(baseDate.AddDays(3), TransactionType.Withdrawal, 1_000m, TransactionStatus.Completed),
            Tx(baseDate.AddDays(4), TransactionType.Recharge, 7_000m, TransactionStatus.Completed),
            // Ruido: movimiento de OTRA billetera, nunca debe aparecer.
            new Transaction { WalletId = Guid.NewGuid(), Type = TransactionType.Recharge, Amount = 99m, CreatedAt = baseDate });
    }

    private Transaction Tx(DateTime at, TransactionType type, decimal amount, TransactionStatus status) => new()
    {
        WalletId = _walletId,
        Type = type,
        Amount = amount,
        Status = status,
        IdempotencyKey = Guid.NewGuid(),
        CreatedAt = at
    };

    private TransactionFilter Filter(
        TransactionType? type = null, TransactionStatus? status = null,
        DateTime? from = null, DateTime? to = null, bool desc = true, int page = 1, int pageSize = 20) =>
        new(_walletId, type, status, from, to, desc, page, pageSize);

    [Fact]
    public async Task SinFiltros_DevuelveSoloLaBilletera_MasRecientePrimero()
    {
        var result = await _service.GetForWalletAsync(Filter());

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(7_000m, result.Items[0].Amount); // el más reciente
        Assert.All(result.Items, t => Assert.Equal(_walletId, t.WalletId));
    }

    [Fact]
    public async Task OrdenAscendente_DevuelveElMasAntiguoPrimero()
    {
        var result = await _service.GetForWalletAsync(Filter(desc: false));

        Assert.Equal(10_000m, result.Items[0].Amount);
    }

    [Fact]
    public async Task FiltroPorTipo_Salidas()
    {
        var result = await _service.GetForWalletAsync(Filter(type: TransactionType.Withdrawal));

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, t => Assert.Equal(TransactionType.Withdrawal, t.Type));
    }

    [Fact]
    public async Task FiltroPorEstado_Failed()
    {
        var result = await _service.GetForWalletAsync(Filter(status: TransactionStatus.Failed));

        Assert.Single(result.Items);
        Assert.Equal(5_000m, result.Items[0].Amount);
    }

    [Fact]
    public async Task FiltroPorFecha_RangoInclusivo()
    {
        var from = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 9, 4, 23, 59, 59, DateTimeKind.Utc);

        var result = await _service.GetForWalletAsync(Filter(from: from, to: to));

        Assert.Equal(3, result.TotalCount); // días 1, 2 y 3 del escalonado
    }

    [Fact]
    public async Task Paginacion_SegundaPaginaDeDos()
    {
        var page2 = await _service.GetForWalletAsync(Filter(page: 2, pageSize: 2));

        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(3, page2.TotalPages);
        Assert.Equal(2, page2.Items.Count);
        Assert.Equal(5_000m, page2.Items[0].Amount); // 3er más reciente
    }

    [Fact]
    public async Task PageSizeFueraDeRango_LanzaValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.GetForWalletAsync(Filter(pageSize: TransactionQueryService.MaxPageSize + 1)));
    }

    [Fact]
    public async Task FromPosteriorATo_LanzaValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.GetForWalletAsync(Filter(from: DateTime.UtcNow, to: DateTime.UtcNow.AddDays(-1))));
    }

    [Fact]
    public async Task BilleteraInexistente_LanzaNotFound()
    {
        var filter = new TransactionFilter(Guid.NewGuid(), null, null, null, null, true, 1, 20);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetForWalletAsync(filter));
    }

    [Fact]
    public async Task GetByIdAsync_Inexistente_LanzaNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(Guid.NewGuid()));
    }
}
