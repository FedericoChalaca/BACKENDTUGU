using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using Tugu.Application.Common.Exceptions;
using Tugu.Application.Common.Interfaces;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

namespace Tugu.Application.Biometrics;

public class BiometricService
{
    private readonly IBiometricRepository _biometrics;
    private readonly IUserRepository _users;
    private readonly ITemplateCipher _cipher;
    private readonly ILogger<BiometricService> _logger;

    public BiometricService(
        IBiometricRepository biometrics,
        IUserRepository users,
        ITemplateCipher cipher,
        ILogger<BiometricService> logger)
    {
        _biometrics = biometrics;
        _users = users;
        _cipher = cipher;
        _logger = logger;
    }

    /// <summary>Enrola la huella de un usuario. Una sola huella activa por usuario.</summary>
    public async Task<Biometric> EnrollAsync(
        Guid userId, byte[] template, string templateFormat, Guid? deviceId, CancellationToken ct = default)
    {
        if (template.Length == 0)
            throw new ValidationException("El template biométrico es obligatorio.");
        if (string.IsNullOrWhiteSpace(templateFormat))
            throw new ValidationException("templateFormat es obligatorio.");

        if (await _users.GetByIdAsync(userId, ct) is null)
            throw new NotFoundException($"No existe un usuario con id {userId}.");

        if (await _biometrics.GetByUserIdAsync(userId, ct) is not null)
            throw new ConflictException(
                "El usuario ya tiene una huella enrolada. Revoca la actual antes de enrolar una nueva.");

        var biometric = new Biometric
        {
            UserId = userId,
            EncryptedTemplate = _cipher.Encrypt(template),
            TemplateFormat = templateFormat.Trim(),
            Status = BiometricStatus.Active,
            EnrolledAt = DateTime.UtcNow,
            EnrolledDeviceId = deviceId,
            CreatedBy = deviceId?.ToString() ?? "api"
        };

        await _biometrics.AddAsync(biometric, ct);

        // Log solo de metadatos: JAMÁS el template, ni encriptado.
        _logger.LogInformation("Huella enrolada para usuario {UserId}", userId);

        return biometric;
    }

    /// <summary>
    /// Identificación 1:N: busca qué usuario enrolado corresponde al template
    /// escaneado. Devuelve null si ninguno coincide.
    /// </summary>
    public async Task<Biometric?> VerifyAsync(byte[] scannedTemplate, CancellationToken ct = default)
    {
        if (scannedTemplate.Length == 0)
            throw new ValidationException("El template escaneado es obligatorio.");

        var candidates = await _biometrics.GetAllActiveAsync(ct);

        foreach (var candidate in candidates)
        {
            var decrypted = _cipher.Decrypt(candidate.EncryptedTemplate);

            if (decrypted.Length == scannedTemplate.Length &&
                CryptographicOperations.FixedTimeEquals(decrypted, scannedTemplate))
            {
                _logger.LogInformation("Verificación biométrica: coincidencia con usuario {UserId}", candidate.UserId);
                return candidate;
            }
        }

        _logger.LogInformation(
            "Verificación biométrica: sin coincidencia entre {Count} huellas activas", candidates.Count);
        return null;
    }

    /// <summary>Estado de enrolamiento de un usuario. Null = aún no enrolado (no es un error).</summary>
    public async Task<Biometric?> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        if (await _users.GetByIdAsync(userId, ct) is null)
            throw new NotFoundException($"No existe un usuario con id {userId}.");

        return await _biometrics.GetByUserIdAsync(userId, ct);
    }
}
