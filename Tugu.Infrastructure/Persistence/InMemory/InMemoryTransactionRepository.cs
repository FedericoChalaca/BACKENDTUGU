using System.Collections.Concurrent;
using Tugu.Application.Common.Interfaces;
using Tugu.Application.Common.Models;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

namespace Tugu.Infrastructure.Persistence.InMemory;

/// <summary>Solo para tests unitarios del servicio de consulta y de reportes.</summary>
public class InMemoryTransactionRepository : ITransactionRepository
{
    private readonly ConcurrentDictionary<Guid, Transaction> _store = new();

    /// <summary>Carga directa para tests (en producción solo escribe el motor).</summary>
    public void Seed(params Transaction[] transactions)
    {
        foreach (var t in transactions) _store[t.Id] = t;
    }

    public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(id, out var t) ? t : null);

    public Task<PagedResult<Transaction>> QueryAsync(TransactionFilter filter, CancellationToken ct = default)
    {
        var query = _store.Values.Where(t => t.WalletId == filter.WalletId);

        if (filter.Type is not null) query = query.Where(t => t.Type == filter.Type);
        if (filter.Status is not null) query = query.Where(t => t.Status == filter.Status);
        if (filter.From is not null) query = query.Where(t => t.CreatedAt >= filter.From);
        if (filter.To is not null) query = query.Where(t => t.CreatedAt <= filter.To);

        var all = query.ToList();
        var ordered = filter.Descending
            ? all.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            : all.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id);

        var items = ordered.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList();
        return Task.FromResult(new PagedResult<Transaction>(items, filter.Page, filter.PageSize, all.Count));
    }

    public Task<PagedResult<Transaction>> ReportAsync(ReportFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        var all = ApplyReportFilter(filter).OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id).ToList();
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new PagedResult<Transaction>(items, page, pageSize, all.Count));
    }

    public Task<TransactionSummary> SummarizeAsync(ReportFilter filter, CancellationToken ct = default)
    {
        var all = ApplyReportFilter(filter).ToList();
        var completed = all.Where(t => t.Status == TransactionStatus.Completed).ToList();

        return Task.FromResult(new TransactionSummary(
            all.Count,
            completed.Where(t => t.Type == TransactionType.Recharge).Sum(t => t.Amount),
            completed.Where(t => t.Type == TransactionType.Withdrawal).Sum(t => t.Amount),
            all.GroupBy(t => t.Status).ToDictionary(g => g.Key, g => g.Count())));
    }

    private IEnumerable<Transaction> ApplyReportFilter(ReportFilter f)
    {
        var query = _store.Values.AsEnumerable();

        if (f.WalletId is not null) query = query.Where(t => t.WalletId == f.WalletId);
        // En memoria la navegación Wallet puede no estar cargada: se resuelve por el CompanyId de la wallet.
        if (f.CompanyId is not null) query = query.Where(t => t.Wallet?.CompanyId == f.CompanyId);
        if (f.DeviceId is not null) query = query.Where(t => t.DeviceId == f.DeviceId);
        if (f.Type is not null) query = query.Where(t => t.Type == f.Type);
        if (f.Status is not null) query = query.Where(t => t.Status == f.Status);
        if (f.From is not null) query = query.Where(t => t.CreatedAt >= f.From);
        if (f.To is not null) query = query.Where(t => t.CreatedAt <= f.To);

        return query;
    }
}
