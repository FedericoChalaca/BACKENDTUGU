using Tugu.Domain.Common;
using Tugu.Domain.Enums;

namespace Tugu.Domain.Entities;

/// <summary>
/// Template de huella enrolado. Una sola huella por usuario (índice único
/// sobre UserId). Dato sensible bajo la Ley 1581: el template se guarda
/// encriptado y JAMÁS se escribe en logs, ni encriptado ni en claro.
/// </summary>
public class Biometric : AuditableEntity
{
    public Guid UserId { get; set; }

    public User? User { get; set; }

    /// <summary>Template encriptado en reposo. La llave vive fuera de la BD.</summary>
    public byte[] EncryptedTemplate { get; set; } = Array.Empty<byte>();

    /// <summary>Formato/algoritmo del SDK del lector, para no atarse a un fabricante.</summary>
    public string TemplateFormat { get; set; } = string.Empty;

    public BiometricStatus Status { get; set; } = BiometricStatus.Active;

    /// <summary>Cuándo se completó el enrolamiento (UTC).</summary>
    public DateTime? EnrolledAt { get; set; }

    /// <summary>Datáfono donde se enroló la huella.</summary>
    public Guid? EnrolledDeviceId { get; set; }
}
