namespace Tugu.Application.Common.Exceptions;

/// <summary>
/// Base de errores de negocio. El middleware de la API las traduce al formato
/// estándar ApiResponse con el código y status HTTP correspondientes.
/// </summary>
public abstract class AppException : Exception
{
    public abstract string Code { get; }

    protected AppException(string message) : base(message)
    {
    }
}

/// <summary>Datos de entrada inválidos → HTTP 400.</summary>
public class ValidationException : AppException
{
    public override string Code => "VALIDATION_ERROR";

    public IDictionary<string, string[]>? Details { get; }

    public ValidationException(string message, IDictionary<string, string[]>? details = null)
        : base(message)
    {
        Details = details;
    }
}

/// <summary>Recurso inexistente → HTTP 404.</summary>
public class NotFoundException : AppException
{
    public override string Code => "NOT_FOUND";

    public NotFoundException(string message) : base(message)
    {
    }
}

/// <summary>Conflicto con el estado actual (duplicados, etc.) → HTTP 409.</summary>
public class ConflictException : AppException
{
    public override string Code => "CONFLICT";

    public ConflictException(string message) : base(message)
    {
    }
}

/// <summary>Saldo insuficiente para un retiro/cobro → HTTP 409.</summary>
public class InsufficientFundsException : ConflictException
{
    public override string Code => "INSUFFICIENT_FUNDS";

    public InsufficientFundsException(string message) : base(message)
    {
    }
}

/// <summary>Sin identidad válida → HTTP 401.</summary>
public class UnauthenticatedException : AppException
{
    public override string Code => "UNAUTHENTICATED";

    public UnauthenticatedException(string message) : base(message)
    {
    }
}
