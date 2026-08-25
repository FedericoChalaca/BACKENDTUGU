namespace Tugu.Contracts.Health;

public class HealthResponse
{
    public required string Status { get; init; }

    public required DateTime Timestamp { get; init; }
}
