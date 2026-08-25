using Microsoft.EntityFrameworkCore;
using Tugu.Application.Common.Interfaces;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

namespace Tugu.Infrastructure.Persistence.Repositories;

public class EfUserRepository : IUserRepository
{
    private readonly TuguDbContext _db;

    public EfUserRepository(TuguDbContext db)
    {
        _db = db;
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByDocumentAsync(DocumentType documentType, string documentNumber, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(
            u => u.DocumentType == documentType && u.DocumentNumber == documentNumber, ct);

    public Task<User?> GetByPhoneAsync(string phoneNumber, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber, ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
    }
}
