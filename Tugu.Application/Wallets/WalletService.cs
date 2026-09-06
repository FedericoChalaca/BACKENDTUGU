using Tugu.Application.Common.Exceptions;
using Tugu.Application.Common.Interfaces;
using Tugu.Domain.Entities;

namespace Tugu.Application.Wallets;

public class WalletService
{
    private readonly IWalletRepository _wallets;
    private readonly IUserRepository _users;

    public WalletService(IWalletRepository wallets, IUserRepository users)
    {
        _wallets = wallets;
        _users = users;
    }

    public async Task<Wallet> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _wallets.GetByIdAsync(id, ct)
               ?? throw new NotFoundException($"No existe una billetera con id {id}.");
    }

    public async Task<Wallet> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _wallets.GetByUserIdAsync(userId, ct)
               ?? throw new NotFoundException("El usuario no tiene una billetera creada.");
    }

    public async Task<Wallet> CreateAsync(Guid userId, CancellationToken ct = default)
    {
        if (await _users.GetByIdAsync(userId, ct) is null)
            throw new NotFoundException($"No existe un usuario con id {userId}.");

        if (await _wallets.GetByUserIdAsync(userId, ct) is not null)
            throw new ConflictException("El usuario ya tiene una billetera.");

        var wallet = new Wallet
        {
            UserId = userId,
            Balance = 0m,
            Currency = "COP",
            CreatedBy = userId.ToString()
        };

        await _wallets.AddAsync(wallet, ct);
        return wallet;
    }
}
