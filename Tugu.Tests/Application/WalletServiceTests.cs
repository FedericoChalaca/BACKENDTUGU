using Tugu.Application.Common.Exceptions;
using Tugu.Application.Common.Interfaces;
using Tugu.Application.Users;
using Tugu.Application.Wallets;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;
using Tugu.Infrastructure.Persistence.InMemory;

namespace Tugu.Tests.Application;

public class WalletServiceTests
{
    private readonly IUserRepository _users = new InMemoryUserRepository();
    private readonly WalletService _service;

    public WalletServiceTests()
    {
        _service = new WalletService(new InMemoryWalletRepository(), _users, new InMemoryCompanyRepository());
    }

    private async Task<User> SeedUserAsync()
    {
        var user = await new UserService(_users).CreateAsync(
            DocumentType.CC, "123456789", "Ana", "Gómez", "3001234567", null);
        return user;
    }

    [Fact]
    public async Task CreateAsync_UsuarioExistente_CreaBilleteraEnCeroCOP()
    {
        var user = await SeedUserAsync();

        var wallet = await _service.CreateAsync(user.Id);

        Assert.Equal(user.Id, wallet.UserId!.Value);
        Assert.Equal(WalletOwnerType.User, wallet.OwnerType);
        Assert.Equal(0m, wallet.Balance);
        Assert.Equal("COP", wallet.Currency);
        Assert.Equal(WalletStatus.Active, wallet.Status);
    }

    [Fact]
    public async Task CreateAsync_UsuarioInexistente_LanzaNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateAsync_SegundaBilletera_LanzaConflict()
    {
        var user = await SeedUserAsync();
        await _service.CreateAsync(user.Id);

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(user.Id));
    }

    [Fact]
    public async Task GetByUserIdAsync_SinBilletera_LanzaNotFound()
    {
        var user = await SeedUserAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByUserIdAsync(user.Id));
    }
}
