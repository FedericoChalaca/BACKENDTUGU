using Microsoft.AspNetCore.Mvc;
using Tugu.Application.Biometrics;
using Tugu.Application.Common.Exceptions;
using Tugu.Application.Users;
using Tugu.Contracts.Biometrics;
using Tugu.Contracts.Common;

namespace Tugu.Api.Controllers;

[ApiController]
[Route("biometrics")]
public class BiometricsController : ControllerBase
{
    private readonly BiometricService _biometricService;
    private readonly UserService _userService;

    public BiometricsController(BiometricService biometricService, UserService userService)
    {
        _biometricService = biometricService;
        _userService = userService;
    }

    /// <summary>Enrola la huella de un usuario (una sola huella activa por usuario).</summary>
    [HttpPost("enroll")]
    [ProducesResponseType(typeof(ApiResponse<EnrollBiometricResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enroll([FromBody] EnrollBiometricRequest request, CancellationToken ct)
    {
        var template = DecodeBase64(request.TemplateBase64, "templateBase64");

        var biometric = await _biometricService.EnrollAsync(
            request.UserId, template, request.TemplateFormat, request.DeviceId, ct);

        var response = new EnrollBiometricResponse
        {
            Id = biometric.Id,
            UserId = biometric.UserId,
            Status = biometric.Status.ToString(),
            EnrolledAt = biometric.EnrolledAt
        };

        return StatusCode(StatusCodes.Status201Created, ApiResponse<EnrollBiometricResponse>.Ok(response));
    }

    /// <summary>
    /// Identifica al usuario SOLO por su huella (1:N, sin QR ni documento).
    /// Siempre HTTP 200: "matched" indica si hubo coincidencia.
    /// </summary>
    [HttpPost("verify")]
    [ProducesResponseType(typeof(ApiResponse<VerifyBiometricResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Verify([FromBody] VerifyBiometricRequest request, CancellationToken ct)
    {
        var template = DecodeBase64(request.TemplateBase64, "templateBase64");

        var match = await _biometricService.VerifyAsync(template, ct);

        if (match is null)
            return Ok(ApiResponse<VerifyBiometricResponse>.Ok(new VerifyBiometricResponse { Matched = false }));

        var user = await _userService.GetByIdAsync(match.UserId, ct);

        return Ok(ApiResponse<VerifyBiometricResponse>.Ok(new VerifyBiometricResponse
        {
            Matched = true,
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName
        }));
    }

    /// <summary>Estado de enrolamiento biométrico de un usuario.</summary>
    [HttpGet("status/{userId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BiometricStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Status(Guid userId, CancellationToken ct)
    {
        var biometric = await _biometricService.GetStatusAsync(userId, ct);

        var response = new BiometricStatusResponse
        {
            UserId = userId,
            Status = biometric?.Status.ToString() ?? "NotEnrolled",
            EnrolledAt = biometric?.EnrolledAt
        };

        return Ok(ApiResponse<BiometricStatusResponse>.Ok(response));
    }

    private static byte[] DecodeBase64(string value, string fieldName)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            throw new ValidationException($"{fieldName} no es un base64 válido.");
        }
    }
}
