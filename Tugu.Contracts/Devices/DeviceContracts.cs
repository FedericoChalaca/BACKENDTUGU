namespace Tugu.Contracts.Devices;

public class RegisterDeviceRequest
{
    public required string SerialNumber { get; init; }

    public string? Alias { get; init; }

    /// <summary>Comercio (corresponsal) que operará el datáfono. Opcional al registrar.</summary>
    public Guid? CompanyId { get; init; }
}

public class AssignDeviceCompanyRequest
{
    public required Guid CompanyId { get; init; }
}

public class DeviceResponse
{
    public required Guid Id { get; init; }

    public required string SerialNumber { get; init; }

    public required string Alias { get; init; }

    public required string Status { get; init; }

    /// <summary>Comercio (corresponsal) asignado; null si aún no tiene.</summary>
    public Guid? CompanyId { get; init; }

    /// <summary>Última señal de vida del datáfono (UTC).</summary>
    public DateTime? LastSeenAt { get; init; }

    public required DateTime CreatedAt { get; init; }
}
