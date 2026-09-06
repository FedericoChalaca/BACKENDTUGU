namespace Tugu.Contracts.Transactions;

public class RechargeRequest
{
    public required Guid WalletId { get; init; }

    public required decimal Amount { get; init; }

    /// <summary>
    /// UUID generado por el cliente ANTES de enviar. Si reintenta la misma
    /// operación, debe reenviar la MISMA clave; el saldo solo se mueve una vez.
    /// </summary>
    public required Guid IdempotencyKey { get; init; }

    public string? Reference { get; init; }

    /// <summary>Datáfono que origina la recarga; null si viene de la app.</summary>
    public Guid? DeviceId { get; init; }
}

/// <summary>
/// Retiro en punto de venta. A diferencia de la recarga, el datáfono
/// (deviceId) es obligatorio: el efectivo lo entrega un corresponsal físico.
/// </summary>
public class WithdrawRequest
{
    public required Guid WalletId { get; init; }

    public required decimal Amount { get; init; }

    /// <summary>UUID generado por el cliente; reintentar con la misma clave no duplica el retiro.</summary>
    public required Guid IdempotencyKey { get; init; }

    public string? Reference { get; init; }

    /// <summary>Datáfono (corresponsal) donde se entrega el efectivo. Obligatorio.</summary>
    public required Guid DeviceId { get; init; }
}

public class TransactionResponse
{
    public required Guid Id { get; init; }

    public required Guid WalletId { get; init; }

    public required string Type { get; init; }

    public required decimal Amount { get; init; }

    public required decimal BalanceAfter { get; init; }

    public required string Status { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public string? Reference { get; init; }

    public Guid? DeviceId { get; init; }

    public required DateTime CreatedAt { get; init; }

    /// <summary>True si esta respuesta es la repetición idempotente de una operación ya procesada.</summary>
    public required bool WasReplay { get; init; }
}
