using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Tugu.Application.Common.Interfaces;

namespace Tugu.Infrastructure.Security;

/// <summary>
/// Encriptación simétrica AES-256-CBC del template biométrico.
///
/// La clave se lee de configuración (Development). En producción debe venir
/// de un KMS (AWS KMS/Secrets Manager) — eso es fase de infraestructura,
/// no de aquí. El IV se genera por operación y va como prefijo del blob
/// guardado (patrón estándar: IV no es secreto, solo debe ser único).
/// </summary>
public class AesTemplateCipher : ITemplateCipher
{
    private const int IvLengthBytes = 16;

    private readonly byte[] _key;

    public AesTemplateCipher(IConfiguration configuration)
    {
        var base64Key = configuration["Biometrics:EncryptionKey"]
            ?? throw new InvalidOperationException(
                "Falta 'Biometrics:EncryptionKey' en la configuración (clave AES-256 en base64).");

        _key = Convert.FromBase64String(base64Key);

        if (_key.Length != 32)
            throw new InvalidOperationException(
                "Biometrics:EncryptionKey debe decodificar a 32 bytes (AES-256).");
    }

    public byte[] Encrypt(byte[] plainTemplate)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var cipherBytes = encryptor.TransformFinalBlock(plainTemplate, 0, plainTemplate.Length);

        return [.. aes.IV, .. cipherBytes];
    }

    public byte[] Decrypt(byte[] encryptedTemplate)
    {
        if (encryptedTemplate.Length < IvLengthBytes)
            throw new CryptographicException("Template encriptado corrupto o incompleto.");

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = encryptedTemplate[..IvLengthBytes];

        using var decryptor = aes.CreateDecryptor();
        var cipherBytes = encryptedTemplate[IvLengthBytes..];
        return decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
    }
}
