namespace Tugu.Contracts.Biometrics;

public class EnrollBiometricRequest
{
    public required Guid UserId { get; init; }

    /// <summary>Template capturado por el SDK del lector, codificado en base64.</summary>
    public required string TemplateBase64 { get; init; }

    /// <summary>Formato/algoritmo del SDK, ej. "ISO-19794-2" o el propio del fabricante.</summary>
    public required string TemplateFormat { get; init; }

    /// <summary>Datáfono donde se enroló; null si fue desde una app.</summary>
    public Guid? DeviceId { get; init; }
}

public class EnrollBiometricResponse
{
    public required Guid Id { get; init; }

    public required Guid UserId { get; init; }

    public required string Status { get; init; }

    public DateTime? EnrolledAt { get; init; }
}

public class VerifyBiometricRequest
{
    /// <summary>
    /// Template capturado en el punto de venta, base64. Sin userId: la
    /// identificación es 1:N, solo con la huella.
    /// </summary>
    public required string TemplateBase64 { get; init; }

    public Guid? DeviceId { get; init; }
}

public class VerifyBiometricResponse
{
    public required bool Matched { get; init; }

    public Guid? UserId { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }
}

public class BiometricStatusResponse
{
    public required Guid UserId { get; init; }

    /// <summary>NotEnrolled / Active / Revoked.</summary>
    public required string Status { get; init; }

    public DateTime? EnrolledAt { get; init; }
}
