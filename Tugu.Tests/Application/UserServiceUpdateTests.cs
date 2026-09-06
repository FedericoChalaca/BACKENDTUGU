using Tugu.Application.Common.Exceptions;
using Tugu.Application.Users;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;
using Tugu.Infrastructure.Persistence.InMemory;

namespace Tugu.Tests.Application;

public class UserServiceUpdateTests
{
    private readonly UserService _service = new(new InMemoryUserRepository());

    private Task<User> SeedAsync(string suffix = "1") =>
        _service.CreateAsync(DocumentType.CC, $"DOC-{suffix}", "Ana", "Gómez", $"300-{suffix}", "ana@test.com");

    [Fact]
    public async Task UpdateAsync_SoloCambiaLosCamposEnviados()
    {
        var user = await SeedAsync();

        var updated = await _service.UpdateAsync(user.Id, firstName: "Anita", lastName: null, phoneNumber: null, email: null);

        Assert.Equal("Anita", updated.FirstName);
        Assert.Equal("Gómez", updated.LastName);           // no se tocó
        Assert.Equal("ana@test.com", updated.Email);       // no se tocó
        Assert.Equal("DOC-1", updated.DocumentNumber);     // documento nunca cambia
        Assert.True(updated.UpdatedAt >= updated.CreatedAt);
    }

    [Fact]
    public async Task UpdateAsync_SinCampos_LanzaValidation()
    {
        var user = await SeedAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateAsync(user.Id, null, null, null, null));
    }

    [Fact]
    public async Task UpdateAsync_NombreVacio_LanzaValidation()
    {
        var user = await SeedAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateAsync(user.Id, firstName: "   ", null, null, null));
    }

    [Fact]
    public async Task UpdateAsync_TelefonoDeOtroUsuario_LanzaConflict()
    {
        var ana = await SeedAsync("a");
        await SeedAsync("b"); // teléfono 300-b

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.UpdateAsync(ana.Id, null, null, phoneNumber: "300-b", null));
    }

    [Fact]
    public async Task UpdateAsync_MismoTelefonoPropio_NoEsConflicto()
    {
        var ana = await SeedAsync("a");

        var updated = await _service.UpdateAsync(ana.Id, null, null, phoneNumber: "300-a", null);

        Assert.Equal("300-a", updated.PhoneNumber);
    }

    [Fact]
    public async Task UpdateAsync_EmailVacio_LoBorra()
    {
        var user = await SeedAsync();

        var updated = await _service.UpdateAsync(user.Id, null, null, null, email: "");

        Assert.Null(updated.Email);
    }

    [Fact]
    public async Task UpdateAsync_UsuarioInexistente_LanzaNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(Guid.NewGuid(), "X", null, null, null));
    }
}
