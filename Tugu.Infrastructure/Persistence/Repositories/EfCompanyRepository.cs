using Microsoft.EntityFrameworkCore;
using Tugu.Application.Common.Interfaces;
using Tugu.Domain.Entities;

namespace Tugu.Infrastructure.Persistence.Repositories;

public class EfCompanyRepository : ICompanyRepository
{
    private readonly TuguDbContext _db;

    public EfCompanyRepository(TuguDbContext db)
    {
        _db = db;
    }

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Companies.Include(c => c.Members).Include(c => c.Wallet)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Company?> GetByNitAsync(string nit, CancellationToken ct = default) =>
        _db.Companies.FirstOrDefaultAsync(c => c.Nit == nit, ct);

    public Task<Company?> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.Companies.Include(c => c.Members).Include(c => c.Wallet)
            .FirstOrDefaultAsync(c => c.Members.Any(m => m.UserId == userId), ct);

    public async Task AddAsync(Company company, CancellationToken ct = default)
    {
        _db.Companies.Add(company);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Company company, CancellationToken ct = default)
    {
        _db.Companies.Update(company);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddMemberAsync(CompanyMember member, CancellationToken ct = default)
    {
        _db.CompanyMembers.Add(member);
        await _db.SaveChangesAsync(ct);
    }
}
