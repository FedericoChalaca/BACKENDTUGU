namespace Tugu.Domain.Common;

/// <summary>
/// Base para toda entidad del dominio. Cumple la regla de auditoría del
/// proyecto: CreatedAt, UpdatedAt y CreatedBy como mínimo en toda entidad.
/// </summary>
public abstract class AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Quién creó el registro: id de usuario, id de dispositivo o "system".</summary>
    public string CreatedBy { get; set; } = "system";
}
