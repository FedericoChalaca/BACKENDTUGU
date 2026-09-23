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
/// Filtro de reportes: cruza billeteras (usuario o comercio) y datáfonos.
/// Todos los criterios son opcionales; sin ninguno, abarca todo el sistema.
/// </summary>
public record ReportFilter(
    Guid? WalletId,
    Guid? CompanyId,
    Guid? DeviceId,
    TransactionType? Type,
    TransactionStatus? Status,
    DateTime? From,
    DateTime? To);

/// <summary>Totales agregados de un conjunto de transacciones.</summary>
public record TransactionSummary(
    int Count,
    decimal TotalIn,
    decimal TotalOut,
    IReadOnlyDictionary<TransactionStatus, int> ByStatus)
{
    public decimal Net => TotalIn - TotalOut;
}

/// <summary>
/// Solo lectura: los movimientos se CREAN únicamente a través del motor
/// transaccional (ITransactionEngine), nunca por un repositorio.
/// </summary>
public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<Transaction>> QueryAsync(TransactionFilter filter, CancellationToken ct = default);

    Task<PagedResult<Transaction>> ReportAsync(ReportFilter filter, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Agrega en la base (SUM/COUNT/GROUP BY), nunca trayendo filas a memoria.</summary>
    Task<TransactionSummary> SummarizeAsync(ReportFilter filter, CancellationToken ct = default);
}
