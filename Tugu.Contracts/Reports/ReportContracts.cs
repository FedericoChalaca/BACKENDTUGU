namespace Tugu.Contracts.Reports;

public class TransactionSummaryResponse
{
    /// <summary>Cantidad de transacciones en el filtro, en cualquier estado.</summary>
    public required int Count { get; init; }

    /// <summary>Suma de recargas COMPLETADAS.</summary>
    public required decimal TotalIn { get; init; }

    /// <summary>Suma de retiros COMPLETADOS.</summary>
    public required decimal TotalOut { get; init; }

    /// <summary>TotalIn − TotalOut.</summary>
    public required decimal Net { get; init; }

    /// <summary>Cantidad por estado: Pending, Completed, Failed, Reversed.</summary>
    public required IReadOnlyDictionary<string, int> ByStatus { get; init; }

    public required string Currency { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }
}
