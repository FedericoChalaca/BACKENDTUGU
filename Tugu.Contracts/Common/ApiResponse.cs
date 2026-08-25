namespace Tugu.Contracts.Common;

/// <summary>
/// Formato estándar de respuesta de la API de TUGU.
/// Toda respuesta (exitosa o de error) usa esta envoltura para que las tres
/// apps cliente (Personal, Negocios, Datáfono) puedan parsearla de forma uniforme.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; init; }

    public T? Data { get; init; }

    public ApiError? Error { get; init; }

    /// <summary>Timestamp UTC de la respuesta.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data) => new()
    {
        Success = true,
        Data = data
    };

    public static ApiResponse<T> Fail(ApiError error) => new()
    {
        Success = false,
        Error = error
    };

    public static ApiResponse<T> Fail(string code, string message) =>
        Fail(new ApiError { Code = code, Message = message });
}

/// <summary>
/// Variante sin cuerpo de datos, para operaciones que solo confirman éxito/error.
/// </summary>
public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Ok() => new() { Success = true };
}
