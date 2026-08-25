namespace Tugu.Application.Common.Interfaces;

/// <summary>
/// Encripta/desencripta templates biométricos en reposo. La implementación
/// real (AES con clave en configuración) vive en Infrastructure; en
/// producción la clave vendrá de un KMS, no de appsettings.
/// </summary>
public interface ITemplateCipher
{
    byte[] Encrypt(byte[] plainTemplate);

    byte[] Decrypt(byte[] encryptedTemplate);
}
