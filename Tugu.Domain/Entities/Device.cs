using Tugu.Domain.Common;
using Tugu.Domain.Enums;

namespace Tugu.Domain.Entities;

/// <summary>
/// Datáfono físico con lector de huella. Sus credenciales de autenticación
/// se agregan en la fase de auth (Cognito/JWT), no aquí.
/// </summary>
public class Device : AuditableEntity
{
    /// <summary>Serial físico del equipo. Único.</summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>Nombre legible, ej. "Datáfono tienda Belén".</summary>
    public string Alias { get; set; } = string.Empty;

    public DeviceStatus Status { get; set; } = DeviceStatus.Active;

    /// <summary>Última vez que el datáfono se comunicó con el backend (UTC).</summary>
    public DateTime? LastSeenAt { get; set; }
}
