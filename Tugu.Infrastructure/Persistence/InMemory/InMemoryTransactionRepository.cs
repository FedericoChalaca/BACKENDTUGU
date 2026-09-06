using System.Collections.Concurrent;
using Tugu.Application.Common.Interfaces;
using Tugu.Application.Common.Models;
using Tugu.Domain.Entities;

namespace Tugu.Infrastructure.Persistence.InMemory;

/// <summary>Solo para tests unitarios del servicio de consulta.</summary>
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
}
