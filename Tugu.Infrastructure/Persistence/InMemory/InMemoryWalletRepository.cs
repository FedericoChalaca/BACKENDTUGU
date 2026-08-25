using System.Collections.Concurrent;
using Tugu.Application.Common.Interfaces;
using Tugu.Domain.Entities;

namespace Tugu.Infrastructure.Persistence.InMemory;

/// <summary>TEMPORAL: ver nota en InMemoryUserRepository.</summary>
public class InMemoryWalletRepository : IWalletRepository
{
    private readonly ConcurrentDictionary<Guid, Wallet> _store = new();

    public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(id, out var wallet) ? wallet : null);

    public Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(_store.Values.FirstOrDefault(w => w.UserId == userId));

    public Task AddAsync(Wallet wallet, CancellationToken ct = default)
    {
        _store[wallet.Id] = wallet;
        return Task.CompletedTask;
    }
}
