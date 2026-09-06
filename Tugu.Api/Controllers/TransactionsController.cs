using Microsoft.AspNetCore.Mvc;
using Tugu.Api.Auth;
using Tugu.Application.Common.Exceptions;
using Tugu.Application.Common.Interfaces;
using Tugu.Application.Transactions;
using Tugu.Application.Wallets;
using Tugu.Contracts.Common;
using Tugu.Contracts.Transactions;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

namespace Tugu.Api.Controllers;

[ApiController]
[Route("transactions")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionEngine _engine;
    private readonly TransactionQueryService _queries;
    private readonly WalletService _wallets;

    public TransactionsController(
        ITransactionEngine engine, TransactionQueryService queries, WalletService wallets)
    {
        _engine = engine;
        _queries = queries;
        _wallets = wallets;
    }

    /// <summary>
    /// Recarga saldo en una billetera. Idempotente: reintentar con la misma
    /// idempotencyKey devuelve la transacción original sin duplicar el efecto.
    /// </summary>
    [HttpPost("recharge")]
    [ProducesResponseType(typeof(ApiResponse<TransactionResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<TransactionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Recharge([FromBody] RechargeRequest request, CancellationToken ct)
    {
        var command = new TransactionCommand(
            request.WalletId,
            TransactionType.Recharge,
            request.Amount,
            request.IdempotencyKey,
            request.Reference,
            request.DeviceId,
            CreatedBy: request.DeviceId?.ToString() ?? "api");

        return await ExecuteAsync(command, ct);
    }

    /// <summary>
    /// Retiro de efectivo en un datáfono. Idempotente. Exige billetera activa,
    /// usuario verificado (KYC), datáfono activo y saldo suficiente.
    /// </summary>
    [HttpPost("withdraw")]
    [ProducesResponseType(typeof(ApiResponse<TransactionResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<TransactionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Withdraw([FromBody] WithdrawRequest request, CancellationToken ct)
    {
        var command = new TransactionCommand(
            request.WalletId,
            TransactionType.Withdrawal,
            request.Amount,
            request.IdempotencyKey,
            request.Reference,
            request.DeviceId,
            CreatedBy: request.DeviceId.ToString());

        return await ExecuteAsync(command, ct);
    }

    /// <summary>
    /// Movimientos de una billetera, paginados y ordenados por fecha.
    /// Sin walletId, usa la billetera del usuario autenticado.
    /// </summary>
    /// <param name="walletId">Billetera a consultar; por defecto la del usuario autenticado.</param>
    /// <param name="type">Recharge (entrada) o Withdrawal (salida).</param>
    /// <param name="status">Pending, Completed, Failed o Reversed.</param>
    /// <param name="from">Fecha/hora inicial (UTC, inclusive).</param>
    /// <param name="to">Fecha/hora final (UTC, inclusive).</param>
    /// <param name="sort">desc (default, más reciente primero) o asc.</param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<TransactionResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? walletId,
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string sort = "desc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var resolvedWalletId = walletId
            ?? (await _wallets.GetByUserIdAsync(DevIdentity.GetUserId(HttpContext), ct)).Id;

        var filter = new TransactionFilter(
            resolvedWalletId,
            ParseEnum<TransactionType>(type, "type"),
            ParseEnum<TransactionStatus>(status, "status"),
            from is null ? null : DateTime.SpecifyKind(from.Value, DateTimeKind.Utc),
            to is null ? null : DateTime.SpecifyKind(to.Value, DateTimeKind.Utc),
            Descending: !string.Equals(sort, "asc", StringComparison.OrdinalIgnoreCase),
            page,
            pageSize);

        var result = await _queries.GetForWalletAsync(filter, ct);

        var response = new PagedResponse<TransactionResponse>
        {
            Items = result.Items.Select(t => ToResponse(t, wasReplay: false)).ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages
        };

        return Ok(ApiResponse<PagedResponse<TransactionResponse>>.Ok(response));
    }

    /// <summary>Detalle de una transacción.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TransactionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var transaction = await _queries.GetByIdAsync(id, ct);
        return Ok(ApiResponse<TransactionResponse>.Ok(ToResponse(transaction, wasReplay: false)));
    }

    private async Task<IActionResult> ExecuteAsync(TransactionCommand command, CancellationToken ct)
    {
        var result = await _engine.ExecuteAsync(command, ct);

        // 201 si se creó ahora; 200 si es la repetición idempotente de una previa.
        return StatusCode(
            result.WasReplay ? StatusCodes.Status200OK : StatusCodes.Status201Created,
            ApiResponse<TransactionResponse>.Ok(ToResponse(result.Transaction, result.WasReplay)));
    }

    private static TEnum? ParseEnum<TEnum>(string? value, string field) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;

        throw new ValidationException(
            $"{field} inválido: usa uno de {string.Join(", ", Enum.GetNames<TEnum>())}.");
    }

    private static TransactionResponse ToResponse(Transaction t, bool wasReplay) => new()
    {
        Id = t.Id,
        WalletId = t.WalletId,
        Type = t.Type.ToString(),
        Amount = t.Amount,
        BalanceAfter = t.BalanceAfter,
        Status = t.Status.ToString(),
        IdempotencyKey = t.IdempotencyKey,
        Reference = t.Reference,
        DeviceId = t.DeviceId,
        CreatedAt = t.CreatedAt,
        WasReplay = wasReplay
    };
}
