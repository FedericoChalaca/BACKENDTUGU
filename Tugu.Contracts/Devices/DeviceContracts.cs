namespace Tugu.Contracts.Devices;

public class RegisterDeviceRequest
{
    public required string SerialNumber { get; init; }

    public string? Alias { get; init; }
}

public class DeviceResponse
{
    public required Guid Id { get; init; }

    public required string SerialNumber { get; init; }

    public required string Alias { get; init; }

    public required string Status { get; init; }

    public required DateTime CreatedAt { get; init; }
}
