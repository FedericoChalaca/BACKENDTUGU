using Microsoft.AspNetCore.Mvc;
using Tugu.Application.Devices;
using Tugu.Contracts.Common;
using Tugu.Contracts.Devices;
using Tugu.Domain.Entities;

namespace Tugu.Api.Controllers;

[ApiController]
[Route("devices")]
public class DevicesController : ControllerBase
{
    private readonly DeviceService _deviceService;

    public DevicesController(DeviceService deviceService)
    {
        _deviceService = deviceService;
    }

    /// <summary>Registra un datáfono físico por su serial.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<DeviceResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request, CancellationToken ct)
    {
        var device = await _deviceService.RegisterAsync(
            request.SerialNumber, request.Alias ?? string.Empty, request.CompanyId, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<DeviceResponse>.Ok(ToResponse(device)));
    }

    /// <summary>Asigna el datáfono a un comercio (corresponsal).</summary>
    [HttpPut("{id:guid}/company")]
    [ProducesResponseType(typeof(ApiResponse<DeviceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignCompany(Guid id, [FromBody] AssignDeviceCompanyRequest request, CancellationToken ct)
    {
        var device = await _deviceService.AssignToCompanyAsync(id, request.CompanyId, ct);
        return Ok(ApiResponse<DeviceResponse>.Ok(ToResponse(device)));
    }

    /// <summary>Devuelve un datáfono por id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DeviceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var device = await _deviceService.GetByIdAsync(id, ct);
        return Ok(ApiResponse<DeviceResponse>.Ok(ToResponse(device)));
    }

    /// <summary>Señal de vida del datáfono: actualiza su última conexión.</summary>
    [HttpPost("{id:guid}/heartbeat")]
    [ProducesResponseType(typeof(ApiResponse<DeviceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Heartbeat(Guid id, CancellationToken ct)
    {
        var device = await _deviceService.HeartbeatAsync(id, ct);
        return Ok(ApiResponse<DeviceResponse>.Ok(ToResponse(device)));
    }

    /// <summary>Activa un datáfono (puede operar).</summary>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(ApiResponse<DeviceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var device = await _deviceService.ActivateAsync(id, ct);
        return Ok(ApiResponse<DeviceResponse>.Ok(ToResponse(device)));
    }

    /// <summary>Desactiva un datáfono (deja de poder operar).</summary>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(ApiResponse<DeviceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var device = await _deviceService.DeactivateAsync(id, ct);
        return Ok(ApiResponse<DeviceResponse>.Ok(ToResponse(device)));
    }

    private static DeviceResponse ToResponse(Device device) => new()
    {
        Id = device.Id,
        SerialNumber = device.SerialNumber,
        Alias = device.Alias,
        Status = device.Status.ToString(),
        CompanyId = device.CompanyId,
        LastSeenAt = device.LastSeenAt,
        CreatedAt = device.CreatedAt
    };
}
