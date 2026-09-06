using Microsoft.EntityFrameworkCore;
using Tugu.Application.Common.Interfaces;
using Tugu.Application.Common.Models;
using Tugu.Domain.Entities;

namespace Tugu.Infrastructure.Persistence.Repositories;

public class EfTransactionRepository : ITransactionRepository
{
    private readonly TuguDbContext _db;

    public EfTransactionRepository(TuguDbContext db)
    {
        _db = db;
    }

    public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<PagedResult<Transaction>> QueryAsync(TransactionFilter filter, CancellationToken ct = default)
    {
        var query = _db.Transactions.AsNoTracking().Where(t => t.WalletId == filter.WalletId);

        if (filter.Type is not null) query = query.Where(t => t.Type == filter.Type);
        if (filter.Status is not null) query = query.Where(t => t.Status == filter.Status);
        if (filter.From is not null) query = query.Where(t => t.CreatedAt >= filter.From);
        if (filter.To is not null) query = query.Where(t => t.CreatedAt <= filter.To);

        var total = await query.CountAsync(ct);

        query = filter.Descending
            ? query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            : query.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id);

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Transaction>(items, filter.Page, filter.PageSize, total);
    }
}
