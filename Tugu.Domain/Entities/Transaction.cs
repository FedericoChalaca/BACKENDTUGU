using Tugu.Domain.Common;
using Tugu.Domain.Enums;

namespace Tugu.Domain.Entities;

/// <summary>
/// Movimiento de saldo. Inmutable: nunca se edita ni se borra; un error se
/// corrige con una transacción de reversa, no modificando la original.
/// </summary>
public class Transaction : AuditableEntity
{
    public Guid WalletId { get; set; }

    public Wallet? Wallet { get; set; }

    public TransactionType Type { get; set; }

    /// <summary>Siempre positivo; el sentido (entra/sale) lo da Type.</summary>
    public decimal Amount { get; set; }

    /// <summary>Snapshot del saldo resultante, para auditoría y detección de inconsistencias.</summary>
    public decimal BalanceAfter { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    /// <summary>
    /// Clave de idempotencia enviada por el cliente. Índice único en BD:
    /// un reintento con la misma clave devuelve la transacción original
    /// en lugar de mover saldo dos veces.
    /// </summary>
    public Guid IdempotencyKey { get; set; }

    /// <summary>Referencia externa legible (ej. número de recibo o del proveedor de recarga).</summary>
    public string? Reference { get; set; }

    /// <summary>Datáfono que originó la operación; null si vino de una app.</summary>
    public Guid? DeviceId { get; set; }

    public Device? Device { get; set; }
}
