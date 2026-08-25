using Microsoft.Extensions.Logging.Abstractions;
using Tugu.Application.Biometrics;
using Tugu.Application.Common.Exceptions;
using Tugu.Application.Common.Interfaces;
using Tugu.Application.Users;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;
using Tugu.Infrastructure.Persistence.InMemory;

namespace Tugu.Tests.Application;

/// <summary>
/// Cifrador reversible trivial (XOR) para aislar la lógica del servicio del
/// algoritmo de cifrado real (probado aparte). Solo para tests.
/// </summary>
file class FakeCipher : ITemplateCipher
{
    public byte[] Encrypt(byte[] plainTemplate) => plainTemplate.Select(b => (byte)(b ^ 0xFF)).ToArray();
    public byte[] Decrypt(byte[] encryptedTemplate) => encryptedTemplate.Select(b => (byte)(b ^ 0xFF)).ToArray();
}

public class BiometricServiceTests
{
    private readonly IUserRepository _users = new InMemoryUserRepository();
    private readonly BiometricService _service;

    public BiometricServiceTests()
    {
        _service = new BiometricService(
            new InMemoryBiometricRepository(), _users, new FakeCipher(), NullLogger<BiometricService>.Instance);
    }

    private async Task<User> SeedUserAsync(string suffix = "1")
    {
        return await new UserService(_users).CreateAsync(
            DocumentType.CC, $"DOC-{suffix}", "Test", "Bio", $"PHONE-{suffix}", null);
    }

    private static byte[] Template(string content) => System.Text.Encoding.UTF8.GetBytes(content);

    [Fact]
    public async Task EnrollAsync_ConDatosValidos_QuedaActivaYElTemplateSeEncripta()
    {
        var user = await SeedUserAsync();
        var rawTemplate = Template("huella-cruda-de-federico");

        var biometric = await _service.EnrollAsync(user.Id, rawTemplate, "ISO-19794-2", null);

        Assert.Equal(BiometricStatus.Active, biometric.Status);
        Assert.NotNull(biometric.EnrolledAt);
        Assert.NotEqual(rawTemplate, biometric.EncryptedTemplate); // se guardó encriptado, no en claro
    }

    [Fact]
    public async Task EnrollAsync_UsuarioInexistente_LanzaNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.EnrollAsync(Guid.NewGuid(), Template("x"), "ISO-19794-2", null));
    }

    [Fact]
    public async Task EnrollAsync_UsuarioYaEnrolado_LanzaConflict()
    {
        var user = await SeedUserAsync();
        await _service.EnrollAsync(user.Id, Template("primera"), "ISO-19794-2", null);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.EnrollAsync(user.Id, Template("segunda"), "ISO-19794-2", null));
    }

    [Fact]
    public async Task EnrollAsync_TemplateVacio_LanzaValidation()
    {
        var user = await SeedUserAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.EnrollAsync(user.Id, Array.Empty<byte>(), "ISO-19794-2", null));
    }

    [Fact]
    public async Task VerifyAsync_TemplateCoincide_IdentificaAlUsuarioCorrecto()
    {
        var ana = await SeedUserAsync("ana");
        var carlos = await SeedUserAsync("carlos");
        await _service.EnrollAsync(ana.Id, Template("huella-ana"), "ISO-19794-2", null);
        await _service.EnrollAsync(carlos.Id, Template("huella-carlos"), "ISO-19794-2", null);

        var match = await _service.VerifyAsync(Template("huella-ana"));

        Assert.NotNull(match);
        Assert.Equal(ana.Id, match!.UserId);
    }

    [Fact]
    public async Task VerifyAsync_SinCoincidencia_DevuelveNull()
    {
        var user = await SeedUserAsync();
        await _service.EnrollAsync(user.Id, Template("huella-real"), "ISO-19794-2", null);

        var match = await _service.VerifyAsync(Template("huella-de-un-desconocido"));

        Assert.Null(match);
    }

    [Fact]
    public async Task VerifyAsync_TemplateVacio_LanzaValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.VerifyAsync(Array.Empty<byte>()));
    }

    [Fact]
    public async Task GetStatusAsync_UsuarioSinEnrolar_DevuelveNullSinLanzar()
    {
        var user = await SeedUserAsync();

        var status = await _service.GetStatusAsync(user.Id);

        Assert.Null(status);
    }

    [Fact]
    public async Task GetStatusAsync_UsuarioEnrolado_DevuelveActivo()
    {
        var user = await SeedUserAsync();
        await _service.EnrollAsync(user.Id, Template("huella"), "ISO-19794-2", null);

        var status = await _service.GetStatusAsync(user.Id);

        Assert.NotNull(status);
        Assert.Equal(BiometricStatus.Active, status!.Status);
    }

    [Fact]
    public async Task GetStatusAsync_UsuarioInexistente_LanzaNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetStatusAsync(Guid.NewGuid()));
    }
}
