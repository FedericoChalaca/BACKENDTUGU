using Tugu.Application.Common.Exceptions;
using Tugu.Application.Common.Interfaces;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

namespace Tugu.Application.Wallets;

public class WalletService
{
    private readonly IWalletRepository _wallets;
    private readonly IUserRepository _users;
    private readonly ICompanyRepository _companies;

    public WalletService(IWalletRepository wallets, IUserRepository users, ICompanyRepository companies)
    {
        _wallets = wallets;
        _users = users;
        _companies = companies;
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

    public async Task<Wallet> GetByCompanyIdAsync(Guid companyId, CancellationToken ct = default)
    {
        return await _wallets.GetByCompanyIdAsync(companyId, ct)
               ?? throw new NotFoundException("El comercio no tiene una billetera creada.");
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
            OwnerType = WalletOwnerType.User,
            Balance = 0m,
            Currency = "COP",
            CreatedBy = userId.ToString()
        };

        await _wallets.AddAsync(wallet, ct);
        return wallet;
    }

    /// <summary>Billetera de un comercio. Solo un miembro del comercio puede crearla.</summary>
    public async Task<Wallet> CreateForCompanyAsync(Guid companyId, Guid callerUserId, CancellationToken ct = default)
    {
        var company = await _companies.GetByIdAsync(companyId, ct)
            ?? throw new NotFoundException($"No existe un comercio con id {companyId}.");

        if (company.Members.All(m => m.UserId != callerUserId))
            throw new ForbiddenException("Solo los usuarios asociados al comercio pueden crear su billetera.");

        if (await _wallets.GetByCompanyIdAsync(companyId, ct) is not null)
            throw new ConflictException("El comercio ya tiene una billetera.");

        var wallet = new Wallet
        {
            CompanyId = companyId,
            OwnerType = WalletOwnerType.Company,
            Balance = 0m,
            Currency = "COP",
            CreatedBy = callerUserId.ToString()
        };

        await _wallets.AddAsync(wallet, ct);
        return wallet;
    }
}
