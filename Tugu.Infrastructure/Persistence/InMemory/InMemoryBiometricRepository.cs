using System.Collections.Concurrent;
using Tugu.Application.Common.Interfaces;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

namespace Tugu.Infrastructure.Persistence.InMemory;

/// <summary>TEMPORAL: ver nota en InMemoryUserRepository. Usado por tests unitarios.</summary>
public class InMemoryBiometricRepository : IBiometricRepository
{
    private readonly ConcurrentDictionary<Guid, Biometric> _store = new();

    public Task<Biometric?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(_store.Values.FirstOrDefault(b => b.UserId == userId));

    public Task<List<Biometric>> GetAllActiveAsync(CancellationToken ct = default) =>
        Task.FromResult(_store.Values.Where(b => b.Status == BiometricStatus.Active).ToList());

    public Task AddAsync(Biometric biometric, CancellationToken ct = default)
    {
        _store[biometric.Id] = biometric;
        return Task.CompletedTask;
    }
}
