using Tugu.Application.Common.Exceptions;
using Tugu.Application.Common.Interfaces;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

namespace Tugu.Application.Companies;

public class CompanyService
{
    private readonly ICompanyRepository _companies;
    private readonly IUserRepository _users;

    public CompanyService(ICompanyRepository companies, IUserRepository users)
    {
        _companies = companies;
        _users = users;
    }

    public async Task<Company> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _companies.GetByIdAsync(id, ct)
               ?? throw new NotFoundException($"No existe un comercio con id {id}.");
    }

    /// <summary>Comercio del usuario autenticado (TUGU Negocios).</summary>
    public async Task<Company> GetForUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _companies.GetByMemberUserIdAsync(userId, ct)
               ?? throw new NotFoundException("El usuario no está asociado a ningún comercio.");
    }

    /// <summary>Crea el comercio; el usuario creador queda como primer miembro asociado.</summary>
    public async Task<Company> CreateAsync(
        Guid creatorUserId, string name, string nit, string? email, string? phoneNumber, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
            errors["name"] = new[] { "El nombre del comercio es obligatorio." };

        var normalizedNit = NormalizeNit(nit);
        if (normalizedNit is null)
            errors["nit"] = new[] { "El NIT es obligatorio y solo admite dígitos y un guion (ej. 900123456-7)." };

        if (!string.IsNullOrWhiteSpace(email) && !email.Contains('@'))
            errors["email"] = new[] { "El email no tiene un formato válido." };

        if (errors.Count > 0)
            throw new ValidationException("Datos del comercio inválidos.", errors);

        if (await _users.GetByIdAsync(creatorUserId, ct) is null)
            throw new NotFoundException($"No existe un usuario con id {creatorUserId}.");

        if (await _companies.GetByMemberUserIdAsync(creatorUserId, ct) is not null)
            throw new ConflictException("El usuario ya está asociado a un comercio.");

        if (await _companies.GetByNitAsync(normalizedNit!, ct) is not null)
            throw new ConflictException("Ya existe un comercio con ese NIT.");

        var company = new Company
        {
            Name = name.Trim(),
            Nit = normalizedNit!,
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
            Status = CompanyStatus.PendingVerification,
            CreatedBy = creatorUserId.ToString()
        };
        company.Members.Add(new CompanyMember
        {
            CompanyId = company.Id,
            UserId = creatorUserId,
            CreatedBy = creatorUserId.ToString()
        });

        await _companies.AddAsync(company, ct);
        return company;
    }

    /// <summary>Solo un miembro del comercio puede editarlo. El NIT no es editable (dato tributario).</summary>
    public async Task<Company> UpdateAsync(
        Guid id, Guid callerUserId, string? name, string? email, string? phoneNumber, CancellationToken ct = default)
    {
        if (name is null && email is null && phoneNumber is null)
            throw new ValidationException("Debes enviar al menos un campo para actualizar.");

        var company = await GetByIdAsync(id, ct);
        EnsureMember(company, callerUserId);

        var errors = new Dictionary<string, string[]>();
        if (name is not null && string.IsNullOrWhiteSpace(name))
            errors["name"] = new[] { "El nombre no puede quedar vacío." };
        if (!string.IsNullOrWhiteSpace(email) && !email.Contains('@'))
            errors["email"] = new[] { "El email no tiene un formato válido." };
        if (errors.Count > 0)
            throw new ValidationException("Datos del comercio inválidos.", errors);

        if (name is not null) company.Name = name.Trim();
        if (email is not null) company.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (phoneNumber is not null) company.PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();

        company.UpdatedAt = DateTime.UtcNow;
        await _companies.UpdateAsync(company, ct);
        return company;
    }

    /// <summary>Asocia otro usuario al comercio. Solo un miembro actual puede hacerlo.</summary>
    public async Task<Company> AddMemberAsync(Guid companyId, Guid callerUserId, Guid userId, CancellationToken ct = default)
    {
        var company = await GetByIdAsync(companyId, ct);
        EnsureMember(company, callerUserId);

        if (await _users.GetByIdAsync(userId, ct) is null)
            throw new NotFoundException($"No existe un usuario con id {userId}.");

        if (company.Members.Any(m => m.UserId == userId))
            throw new ConflictException("El usuario ya es miembro de este comercio.");

        if (await _companies.GetByMemberUserIdAsync(userId, ct) is not null)
            throw new ConflictException("El usuario ya está asociado a otro comercio.");

        var member = new CompanyMember { CompanyId = company.Id, UserId = userId, CreatedBy = callerUserId.ToString() };
        await _companies.AddMemberAsync(member, ct);

        // EF puede haber enlazado ya el miembro a la colección (fixup); no duplicar.
        if (company.Members.All(m => m.UserId != userId))
            company.Members.Add(member);

        return company;
    }

    public static bool IsMember(Company company, Guid userId) =>
        company.Members.Any(m => m.UserId == userId);

    private static void EnsureMember(Company company, Guid userId)
    {
        if (!IsMember(company, userId))
            throw new ForbiddenException("Solo los usuarios asociados al comercio pueden administrarlo.");
    }

    /// <summary>Acepta "900.123.456-7", "900123456-7" o "9001234567"; devuelve solo dígitos y guion.</summary>
    private static string? NormalizeNit(string? nit)
    {
        if (string.IsNullOrWhiteSpace(nit)) return null;
        var cleaned = new string(nit.Where(c => char.IsDigit(c) || c == '-').ToArray());
        var digits = cleaned.Count(char.IsDigit);
        return digits is >= 6 and <= 15 && cleaned.Count(c => c == '-') <= 1 ? cleaned : null;
    }
}
