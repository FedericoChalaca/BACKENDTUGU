using Microsoft.AspNetCore.Mvc;
using Tugu.Contracts.Common;
using Tugu.Contracts.Health;

namespace Tugu.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HealthResponse>), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        var health = new HealthResponse
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow
        };

        return Ok(ApiResponse<HealthResponse>.Ok(health));
    }
}
