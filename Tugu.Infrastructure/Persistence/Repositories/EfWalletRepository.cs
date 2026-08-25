using Microsoft.EntityFrameworkCore;
using Tugu.Application.Common.Interfaces;
using Tugu.Domain.Entities;

namespace Tugu.Infrastructure.Persistence.Repositories;

public class EfWalletRepository : IWalletRepository
{
    private readonly TuguDbContext _db;

    public EfWalletRepository(TuguDbContext db)
    {
        _db = db;
    }

    public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Wallets.FirstOrDefaultAsync(w => w.Id == id, ct);

    public Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.Wallets.FirstOrDefaultAsync(w => w.UserId == userId, ct);

    public async Task AddAsync(Wallet wallet, CancellationToken ct = default)
    {
        _db.Wallets.Add(wallet);
        await _db.SaveChangesAsync(ct);
    }
}
