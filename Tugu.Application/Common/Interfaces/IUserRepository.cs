using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

namespace Tugu.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<User?> GetByDocumentAsync(DocumentType documentType, string documentNumber, CancellationToken ct = default);

    Task<User?> GetByPhoneAsync(string phoneNumber, CancellationToken ct = default);

    Task AddAsync(User user, CancellationToken ct = default);
}
