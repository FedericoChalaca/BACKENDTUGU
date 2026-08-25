using Tugu.Application.Common.Exceptions;
using Tugu.Application.Users;
using Tugu.Domain.Enums;
using Tugu.Infrastructure.Persistence.InMemory;

namespace Tugu.Tests.Application;

public class UserServiceTests
{
    private static UserService NewService() => new(new InMemoryUserRepository());

    [Fact]
    public async Task CreateAsync_ConDatosValidos_CreaUsuarioPendienteDeVerificacion()
    {
        var service = NewService();

        var user = await service.CreateAsync(
            DocumentType.CC, "123456789", "Ana", "Gómez", "3001234567", "ana@test.com");

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(UserStatus.PendingVerification, user.Status);
        Assert.Equal("123456789", user.DocumentNumber);
    }

    [Fact]
    public async Task CreateAsync_DocumentoDuplicado_LanzaConflict()
    {
        var service = NewService();
        await service.CreateAsync(DocumentType.CC, "123456789", "Ana", "Gómez", "3001234567", null);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(DocumentType.CC, "123456789", "Otro", "Pérez", "3009999999", null));
    }

    [Fact]
    public async Task CreateAsync_TelefonoDuplicado_LanzaConflict()
    {
        var service = NewService();
        await service.CreateAsync(DocumentType.CC, "123456789", "Ana", "Gómez", "3001234567", null);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(DocumentType.CE, "987654321", "Otro", "Pérez", "3001234567", null));
    }

    [Fact]
    public async Task CreateAsync_SinCamposObligatorios_LanzaValidationConDetalles()
    {
        var service = NewService();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(DocumentType.CC, "", "", "", "", "no-es-email"));

        Assert.NotNull(ex.Details);
        Assert.Contains("documentNumber", ex.Details.Keys);
        Assert.Contains("email", ex.Details.Keys);
    }

    [Fact]
    public async Task GetByIdAsync_Inexistente_LanzaNotFound()
    {
        var service = NewService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(Guid.NewGuid()));
    }
}
