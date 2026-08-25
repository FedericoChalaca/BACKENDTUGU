namespace Tugu.Contracts.Common;

/// <summary>
/// Error estándar de la API. <see cref="Code"/> es un código estable y legible
/// por máquina (ej. "INTERNAL_ERROR", "VALIDATION_ERROR") que los clientes
/// pueden usar para decidir comportamiento; <see cref="Message"/> es para humanos.
/// </summary>
public class ApiError
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    /// <summary>
    /// Detalles opcionales, p. ej. errores de validación por campo.
    /// </summary>
    public IDictionary<string, string[]>? Details { get; init; }
}
