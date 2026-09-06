using Tugu.Application.Common.Models;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

namespace Tugu.Application.Common.Interfaces;

/// <summary>Filtro de consulta de movimientos de una billetera.</summary>
public record TransactionFilter(
    Guid WalletId,
    TransactionType? Type,
    TransactionStatus? Status,
    DateTime? From,
    DateTime? To,
    bool Descending,
    int Page,
    int PageSize);

/// <summary>
/// Solo lectura: los movimientos se CREAN únicamente a través del motor
/// transaccional (ITransactionEngine), nunca por un repositorio.
/// </summary>
public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<Transaction>> QueryAsync(TransactionFilter filter, CancellationToken ct = default);
}
