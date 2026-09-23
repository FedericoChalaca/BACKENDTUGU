using Microsoft.AspNetCore.Mvc;
using Tugu.Application.Common.Exceptions;
using Tugu.Application.Common.Interfaces;
using Tugu.Application.Reports;
using Tugu.Contracts.Common;
using Tugu.Contracts.Reports;
using Tugu.Contracts.Transactions;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

namespace Tugu.Api.Controllers;

/// <summary>
/// Reportes de transacciones por billetera, comercio o datáfono.
/// NOTA: hasta Cognito no hay roles; cualquier caller puede pedir cualquier
/// filtro. Con JWT, el alcance se restringirá al usuario/comercio del token.
/// </summary>
[ApiController]
[Route("reports")]
public class ReportsController : ControllerBase
{
    private readonly ReportService _reports;

    public ReportsController(ReportService reports)
    {
        _reports = reports;
    }

    /// <summary>Totales (entradas, salidas, neto, cantidad, por estado) del filtro.</summary>
    /// <param name="walletId">Billetera (usuario o comercio).</param>
    /// <param name="companyId">Comercio: todas las transacciones de su billetera.</param>
    /// <param name="deviceId">Datáfono (corresponsal) que originó las transacciones.</param>
    /// <param name="from">Desde (UTC, inclusive).</param>
    /// <param name="to">Hasta (UTC, inclusive). Rango máximo: 366 días.</param>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<TransactionSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Summary(
        [FromQuery] Guid? walletId, [FromQuery] Guid? companyId, [FromQuery] Guid? deviceId,
        [FromQuery] string? type, [FromQuery] string? status,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        CancellationToken ct = default)
    {
        var filter = BuildFilter(walletId, companyId, deviceId, type, status, from, to);
        var s = await _reports.SummaryAsync(filter, ct);

        return Ok(ApiResponse<TransactionSummaryResponse>.Ok(new TransactionSummaryResponse
        {
            Count = s.Count,
            TotalIn = s.TotalIn,
            TotalOut = s.TotalOut,
            Net = s.Net,
            ByStatus = s.ByStatus.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            Currency = "COP",
            From = filter.From,
            To = filter.To
        }));
    }

    /// <summary>Listado paginado de transacciones del filtro, más reciente primero.</summary>
    [HttpGet("transactions")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<TransactionResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Transactions(
        [FromQuery] Guid? walletId, [FromQuery] Guid? companyId, [FromQuery] Guid? deviceId,
        [FromQuery] string? type, [FromQuery] string? status,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var filter = BuildFilter(walletId, companyId, deviceId, type, status, from, to);
        var result = await _reports.TransactionsAsync(filter, page, pageSize, ct);

        return Ok(ApiResponse<PagedResponse<TransactionResponse>>.Ok(new PagedResponse<TransactionResponse>
        {
            Items = result.Items.Select(ToResponse).ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages
        }));
    }

    private static ReportFilter BuildFilter(
        Guid? walletId, Guid? companyId, Guid? deviceId, string? type, string? status, DateTime? from, DateTime? to) =>
        new(walletId, companyId, deviceId,
            ParseEnum<TransactionType>(type, "type"),
            ParseEnum<TransactionStatus>(status, "status"),
            from is null ? null : DateTime.SpecifyKind(from.Value, DateTimeKind.Utc),
            to is null ? null : DateTime.SpecifyKind(to.Value, DateTimeKind.Utc));

    private static TEnum? ParseEnum<TEnum>(string? value, string field) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)) return parsed;
        throw new ValidationException($"{field} inválido: usa uno de {string.Join(", ", Enum.GetNames<TEnum>())}.");
    }

    private static TransactionResponse ToResponse(Transaction t) => new()
    {
        Id = t.Id, WalletId = t.WalletId, Type = t.Type.ToString(), Amount = t.Amount,
        BalanceAfter = t.BalanceAfter, Status = t.Status.ToString(), IdempotencyKey = t.IdempotencyKey,
        Reference = t.Reference, DeviceId = t.DeviceId, CreatedAt = t.CreatedAt, WasReplay = false
    };
}
