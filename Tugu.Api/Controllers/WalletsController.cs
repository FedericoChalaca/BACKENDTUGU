using Microsoft.AspNetCore.Mvc;
using Tugu.Api.Auth;
using Tugu.Application.Wallets;
using Tugu.Contracts.Common;
using Tugu.Contracts.Wallets;
using Tugu.Domain.Entities;

namespace Tugu.Api.Controllers;

[ApiController]
[Route("wallets")]
public class WalletsController : ControllerBase
{
    private readonly WalletService _walletService;

    public WalletsController(WalletService walletService)
    {
        _walletService = walletService;
    }

    /// <summary>Crea la billetera de un usuario (una por usuario).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<WalletResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateWalletRequest request, CancellationToken ct)
    {
        var wallet = await _walletService.CreateAsync(request.UserId, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<WalletResponse>.Ok(ToResponse(wallet)));
    }

    /// <summary>Devuelve la billetera del usuario autenticado.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<WalletResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = DevIdentity.GetUserId(HttpContext);
        var wallet = await _walletService.GetByUserIdAsync(userId, ct);
        return Ok(ApiResponse<WalletResponse>.Ok(ToResponse(wallet)));
    }

    /// <summary>Devuelve una billetera por id (incluye el saldo).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<WalletResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var wallet = await _walletService.GetByIdAsync(id, ct);
        return Ok(ApiResponse<WalletResponse>.Ok(ToResponse(wallet)));
    }

    private static WalletResponse ToResponse(Wallet wallet) => new()
    {
        Id = wallet.Id,
        UserId = wallet.UserId,
        Balance = wallet.Balance,
        Currency = wallet.Currency,
        Status = wallet.Status.ToString(),
        CreatedAt = wallet.CreatedAt
    };
}
