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
        var device = await _deviceService.RegisterAsync(request.SerialNumber, request.Alias ?? string.Empty, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<DeviceResponse>.Ok(ToResponse(device)));
    }

    private static DeviceResponse ToResponse(Device device) => new()
    {
        Id = device.Id,
        SerialNumber = device.SerialNumber,
        Alias = device.Alias,
        Status = device.Status.ToString(),
        CreatedAt = device.CreatedAt
    };
}
