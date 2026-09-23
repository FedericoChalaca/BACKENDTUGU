using Microsoft.EntityFrameworkCore;
using Tugu.Application.Common.Interfaces;
using Tugu.Application.Common.Models;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

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

    public async Task<PagedResult<Transaction>> ReportAsync(
        ReportFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        var query = ApplyReportFilter(filter);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<Transaction>(items, page, pageSize, total);
    }

    public async Task<TransactionSummary> SummarizeAsync(ReportFilter filter, CancellationToken ct = default)
    {
        var query = ApplyReportFilter(filter);

        // Un solo GROUP BY en la base: filas = (tipo, estado) → count, sum.
        var groups = await query
            .GroupBy(t => new { t.Type, t.Status })
            .Select(g => new { g.Key.Type, g.Key.Status, Count = g.Count(), Amount = g.Sum(t => t.Amount) })
            .ToListAsync(ct);

        // Los totales de dinero solo cuentan lo que efectivamente se movió.
        var completed = groups.Where(g => g.Status == TransactionStatus.Completed).ToList();

        return new TransactionSummary(
            Count: groups.Sum(g => g.Count),
            TotalIn: completed.Where(g => g.Type == TransactionType.Recharge).Sum(g => g.Amount),
            TotalOut: completed.Where(g => g.Type == TransactionType.Withdrawal).Sum(g => g.Amount),
            ByStatus: groups.GroupBy(g => g.Status).ToDictionary(g => g.Key, g => g.Sum(x => x.Count)));
    }

    private IQueryable<Transaction> ApplyReportFilter(ReportFilter f)
    {
        var query = _db.Transactions.AsNoTracking();

        if (f.WalletId is not null) query = query.Where(t => t.WalletId == f.WalletId);
        if (f.CompanyId is not null) query = query.Where(t => t.Wallet!.CompanyId == f.CompanyId);
        if (f.DeviceId is not null) query = query.Where(t => t.DeviceId == f.DeviceId);
        if (f.Type is not null) query = query.Where(t => t.Type == f.Type);
        if (f.Status is not null) query = query.Where(t => t.Status == f.Status);
        if (f.From is not null) query = query.Where(t => t.CreatedAt >= f.From);
        if (f.To is not null) query = query.Where(t => t.CreatedAt <= f.To);

        return query;
    }
}
