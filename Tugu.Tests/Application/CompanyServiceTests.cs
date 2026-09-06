using Tugu.Application.Common.Exceptions;
using Tugu.Application.Common.Interfaces;
using Tugu.Application.Companies;
using Tugu.Application.Users;
using Tugu.Application.Wallets;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;
using Tugu.Infrastructure.Persistence.InMemory;

namespace Tugu.Tests.Application;

public class CompanyServiceTests
{
    private readonly IUserRepository _users = new InMemoryUserRepository();
    private readonly ICompanyRepository _companies = new InMemoryCompanyRepository();
    private readonly InMemoryWalletRepository _wallets = new();
    private readonly CompanyService _service;
    private readonly WalletService _walletService;

    public CompanyServiceTests()
    {
        _service = new CompanyService(_companies, _users);
        _walletService = new WalletService(_wallets, _users, _companies);
    }

    private Task<User> SeedUserAsync(string suffix) =>
        new UserService(_users).CreateAsync(DocumentType.CC, $"DOC-{suffix}", "Test", "Co", $"300-{suffix}", null);

    [Fact]
    public async Task CreateAsync_Valido_CreaConElCreadorComoMiembroYNitNormalizado()
    {
        var owner = await SeedUserAsync("o");

        var company = await _service.CreateAsync(owner.Id, "  Tienda Belén ", "900.123.456-7", "tienda@test.com", "6041234567");

        Assert.Equal("Tienda Belén", company.Name);
        Assert.Equal("900123456-7", company.Nit);
        Assert.Equal(CompanyStatus.PendingVerification, company.Status);
        Assert.Single(company.Members);
        Assert.Equal(owner.Id, company.Members.First().UserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12")]
    [InlineData("900-123-456-7")]
    public async Task CreateAsync_NitInvalido_LanzaValidation(string nit)
    {
        var owner = await SeedUserAsync("n");

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(owner.Id, "Tienda", nit, null, null));
    }

    [Fact]
    public async Task CreateAsync_NitDuplicado_LanzaConflict()
    {
        var a = await SeedUserAsync("a");
        var b = await SeedUserAsync("b");
        await _service.CreateAsync(a.Id, "Tienda A", "900123456-7", null, null);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.CreateAsync(b.Id, "Tienda B", "900.123.456-7", null, null));
    }

    [Fact]
    public async Task CreateAsync_UsuarioYaAsociadoAOtroComercio_LanzaConflict()
    {
        var owner = await SeedUserAsync("dup");
        await _service.CreateAsync(owner.Id, "Tienda 1", "900000001-1", null, null);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.CreateAsync(owner.Id, "Tienda 2", "900000002-2", null, null));
    }

    [Fact]
    public async Task CreateAsync_UsuarioInexistente_LanzaNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateAsync(Guid.NewGuid(), "Tienda", "900123456-7", null, null));
    }

    [Fact]
    public async Task GetForUserAsync_MiembroYNoMiembro()
    {
        var owner = await SeedUserAsync("m");
        var stranger = await SeedUserAsync("s");
        var company = await _service.CreateAsync(owner.Id, "Tienda", "900123456-7", null, null);

        var found = await _service.GetForUserAsync(owner.Id);
        Assert.Equal(company.Id, found.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetForUserAsync(stranger.Id));
    }

    [Fact]
    public async Task UpdateAsync_MiembroEdita_NoMiembroRecibeForbidden()
    {
        var owner = await SeedUserAsync("u");
        var stranger = await SeedUserAsync("x");
        var company = await _service.CreateAsync(owner.Id, "Tienda", "900123456-7", null, null);

        var updated = await _service.UpdateAsync(company.Id, owner.Id, "Tienda Nueva", "nuevo@test.com", null);
        Assert.Equal("Tienda Nueva", updated.Name);
        Assert.Equal("nuevo@test.com", updated.Email);
        Assert.Equal("900123456-7", updated.Nit); // el NIT nunca cambia

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.UpdateAsync(company.Id, stranger.Id, "Hack", null, null));
    }

    [Fact]
    public async Task UpdateAsync_SinCampos_LanzaValidation()
    {
        var owner = await SeedUserAsync("v");
        var company = await _service.CreateAsync(owner.Id, "Tienda", "900123456-7", null, null);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateAsync(company.Id, owner.Id, null, null, null));
    }

    [Fact]
    public async Task AddMemberAsync_AsociaUsuario_YRechazaDuplicadosYExtraños()
    {
        var owner = await SeedUserAsync("w");
        var employee = await SeedUserAsync("e");
        var stranger = await SeedUserAsync("z");
        var company = await _service.CreateAsync(owner.Id, "Tienda", "900123456-7", null, null);

        var withMember = await _service.AddMemberAsync(company.Id, owner.Id, employee.Id);
        Assert.Equal(2, withMember.Members.Count);
        Assert.Contains(withMember.Members, m => m.UserId == employee.Id);

        await Assert.ThrowsAsync<ConflictException>(() => _service.AddMemberAsync(company.Id, owner.Id, employee.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => _service.AddMemberAsync(company.Id, stranger.Id, stranger.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.AddMemberAsync(company.Id, owner.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task WalletDeComercio_SoloMiembroLaCrea_YUnaSola()
    {
        var owner = await SeedUserAsync("wa");
        var stranger = await SeedUserAsync("wb");
        var company = await _service.CreateAsync(owner.Id, "Tienda", "900123456-7", null, null);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _walletService.CreateForCompanyAsync(company.Id, stranger.Id));

        var wallet = await _walletService.CreateForCompanyAsync(company.Id, owner.Id);
        Assert.Equal(WalletOwnerType.Company, wallet.OwnerType);
        Assert.Equal(company.Id, wallet.CompanyId);
        Assert.Null(wallet.UserId);
        Assert.Equal(0m, wallet.Balance);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _walletService.CreateForCompanyAsync(company.Id, owner.Id));

        var found = await _walletService.GetByCompanyIdAsync(company.Id);
        Assert.Equal(wallet.Id, found.Id);
    }

    [Fact]
    public async Task WalletDeComercio_ComercioInexistente_LanzaNotFound()
    {
        var owner = await SeedUserAsync("nf");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _walletService.CreateForCompanyAsync(Guid.NewGuid(), owner.Id));
    }
}
