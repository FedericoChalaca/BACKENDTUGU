using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

namespace Tugu.Application.Transactions;

/// <summary>Orden para el motor: mover saldo de una billetera.</summary>
public record TransactionCommand(
    Guid WalletId,
    TransactionType Type,
    decimal Amount,
    Guid IdempotencyKey,
    string? Reference,
    Guid? DeviceId,
    string CreatedBy);

/// <summary>
/// Resultado del motor. <see cref="WasReplay"/> es true cuando la
/// idempotencyKey ya se había procesado y se devuelve la transacción original
/// sin mover saldo de nuevo.
/// </summary>
public record TransactionResult(Transaction Transaction, bool WasReplay);

/// <summary>
/// Motor transaccional genérico. Garantiza las tres reglas no negociables:
/// atomicidad (todo-o-nada en una transacción SQL real), idempotencia
/// (índice único sobre idempotency_key) y concurrencia (lock de fila sobre
/// la billetera). La implementación vive en Infrastructure porque necesita
/// la base de datos.
/// </summary>
public interface ITransactionEngine
{
    Task<TransactionResult> ExecuteAsync(TransactionCommand command, CancellationToken ct = default);
}
