using Microsoft.EntityFrameworkCore;
using Tugu.Application.Common.Interfaces;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

namespace Tugu.Infrastructure.Persistence.Repositories;

public class EfBiometricRepository : IBiometricRepository
{
    private readonly TuguDbContext _db;

    public EfBiometricRepository(TuguDbContext db)
    {
        _db = db;
    }

    public Task<Biometric?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.Biometrics.FirstOrDefaultAsync(b => b.UserId == userId, ct);

    public Task<List<Biometric>> GetAllActiveAsync(CancellationToken ct = default) =>
        _db.Biometrics.Where(b => b.Status == BiometricStatus.Active).ToListAsync(ct);

    public async Task AddAsync(Biometric biometric, CancellationToken ct = default)
    {
        _db.Biometrics.Add(biometric);
        await _db.SaveChangesAsync(ct);
    }
}
