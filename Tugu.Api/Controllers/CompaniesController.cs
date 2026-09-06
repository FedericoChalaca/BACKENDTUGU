using Microsoft.AspNetCore.Mvc;
using Tugu.Api.Auth;
using Tugu.Application.Companies;
using Tugu.Contracts.Common;
using Tugu.Contracts.Companies;
using Tugu.Domain.Entities;

namespace Tugu.Api.Controllers;

[ApiController]
[Route("companies")]
public class CompaniesController : ControllerBase
{
    private readonly CompanyService _companyService;

    public CompaniesController(CompanyService companyService)
    {
        _companyService = companyService;
    }

    /// <summary>Crea un comercio; el usuario autenticado queda como primer miembro asociado.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CompanyResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateCompanyRequest request, CancellationToken ct)
    {
        var callerId = DevIdentity.GetUserId(HttpContext);
        var company = await _companyService.CreateAsync(
            callerId, request.Name, request.Nit, request.Email, request.PhoneNumber, ct);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<CompanyResponse>.Ok(ToResponse(company)));
    }

    /// <summary>Comercio del usuario autenticado (TUGU Negocios).</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<CompanyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var callerId = DevIdentity.GetUserId(HttpContext);
        var company = await _companyService.GetForUserAsync(callerId, ct);
        return Ok(ApiResponse<CompanyResponse>.Ok(ToResponse(company)));
    }

    /// <summary>Comercio por id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var company = await _companyService.GetByIdAsync(id, ct);
        return Ok(ApiResponse<CompanyResponse>.Ok(ToResponse(company)));
    }

    /// <summary>Edita nombre/email/teléfono. Solo miembros del comercio. El NIT no es editable.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCompanyRequest request, CancellationToken ct)
    {
        var callerId = DevIdentity.GetUserId(HttpContext);
        var company = await _companyService.UpdateAsync(
            id, callerId, request.Name, request.Email, request.PhoneNumber, ct);

        return Ok(ApiResponse<CompanyResponse>.Ok(ToResponse(company)));
    }

    /// <summary>Asocia otro usuario al comercio. Solo miembros actuales.</summary>
    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(typeof(ApiResponse<CompanyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddCompanyMemberRequest request, CancellationToken ct)
    {
        var callerId = DevIdentity.GetUserId(HttpContext);
        var company = await _companyService.AddMemberAsync(id, callerId, request.UserId, ct);
        return Ok(ApiResponse<CompanyResponse>.Ok(ToResponse(company)));
    }

    private static CompanyResponse ToResponse(Company c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Nit = c.Nit,
        Email = c.Email,
        PhoneNumber = c.PhoneNumber,
        Status = c.Status.ToString(),
        MemberUserIds = c.Members.Select(m => m.UserId).ToList(),
        WalletId = c.Wallet?.Id,
        CreatedAt = c.CreatedAt
    };
}
