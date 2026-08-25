using Microsoft.AspNetCore.Mvc;
using Tugu.Application.Transactions;
using Tugu.Contracts.Common;
using Tugu.Contracts.Transactions;
using Tugu.Domain.Enums;

namespace Tugu.Api.Controllers;

[ApiController]
[Route("transactions")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionEngine _engine;

    public TransactionsController(ITransactionEngine engine)
    {
        _engine = engine;
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

        var result = await _engine.ExecuteAsync(command, ct);

        var response = new TransactionResponse
        {
            Id = result.Transaction.Id,
            WalletId = result.Transaction.WalletId,
            Type = result.Transaction.Type.ToString(),
            Amount = result.Transaction.Amount,
            BalanceAfter = result.Transaction.BalanceAfter,
            Status = result.Transaction.Status.ToString(),
            IdempotencyKey = result.Transaction.IdempotencyKey,
            Reference = result.Transaction.Reference,
            DeviceId = result.Transaction.DeviceId,
            CreatedAt = result.Transaction.CreatedAt,
            WasReplay = result.WasReplay
        };

        // 201 si se creó ahora; 200 si es la repetición idempotente de una previa.
        return StatusCode(
            result.WasReplay ? StatusCodes.Status200OK : StatusCodes.Status201Created,
            ApiResponse<TransactionResponse>.Ok(response));
    }
}
