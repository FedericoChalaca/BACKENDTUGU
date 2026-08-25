using Tugu.Domain.Entities;

namespace Tugu.Application.Common.Interfaces;

public interface IBiometricRepository
{
    Task<Biometric?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Todas las huellas activas, candidatas para identificación 1:N.</summary>
    Task<List<Biometric>> GetAllActiveAsync(CancellationToken ct = default);

    Task AddAsync(Biometric biometric, CancellationToken ct = default);
}
