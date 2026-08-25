using Tugu.Domain.Entities;

namespace Tugu.Application.Common.Interfaces;

public interface IWalletRepository
{
    Task<Wallet?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task AddAsync(Wallet wallet, CancellationToken ct = default);
}
