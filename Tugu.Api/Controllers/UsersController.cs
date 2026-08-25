using Microsoft.AspNetCore.Mvc;
using Tugu.Api.Auth;
using Tugu.Application.Common.Exceptions;
using Tugu.Application.Users;
using Tugu.Contracts.Common;
using Tugu.Contracts.Users;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

namespace Tugu.Api.Controllers;

[ApiController]
[Route("users")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;

    public UsersController(UserService userService)
    {
        _userService = userService;
    }

    /// <summary>Crea un usuario (registro desde TUGU Personal).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(typeof(DocumentType), request.DocumentType))
            throw new ValidationException("documentType inválido: usa 1=CC, 2=CE, 3=TI, 4=Passport.");

        var user = await _userService.CreateAsync(
            (DocumentType)request.DocumentType,
            request.DocumentNumber,
            request.FirstName,
            request.LastName,
            request.PhoneNumber,
            request.Email,
            ct);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<UserResponse>.Ok(ToResponse(user)));
    }

    /// <summary>Devuelve el usuario autenticado.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<UserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = DevIdentity.GetUserId(HttpContext);
        var user = await _userService.GetByIdAsync(userId, ct);
        return Ok(ApiResponse<UserResponse>.Ok(ToResponse(user)));
    }

    private static UserResponse ToResponse(User user) => new()
    {
        Id = user.Id,
        DocumentType = user.DocumentType.ToString(),
        DocumentNumber = user.DocumentNumber,
        FirstName = user.FirstName,
        LastName = user.LastName,
        PhoneNumber = user.PhoneNumber,
        Email = user.Email,
        Status = user.Status.ToString(),
        CreatedAt = user.CreatedAt
    };
}
