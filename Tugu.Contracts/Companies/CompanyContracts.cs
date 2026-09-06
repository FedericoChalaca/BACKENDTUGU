namespace Tugu.Contracts.Companies;

public class CreateCompanyRequest
{
    public required string Name { get; init; }

    /// <summary>NIT colombiano, ej. "900123456-7". Se acepta con o sin puntos.</summary>
    public required string Nit { get; init; }

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }
}

/// <summary>Todos opcionales; solo se actualizan los enviados. El NIT no es editable.</summary>
public class UpdateCompanyRequest
{
    public string? Name { get; init; }

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }
}

public class AddCompanyMemberRequest
{
    public required Guid UserId { get; init; }
}

public class CompanyResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Nit { get; init; }

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    /// <summary>PendingVerification / Active / Blocked.</summary>
    public required string Status { get; init; }

    /// <summary>Usuarios asociados que administran el comercio.</summary>
    public required IReadOnlyList<Guid> MemberUserIds { get; init; }

    /// <summary>Billetera del comercio; null hasta que se cree con POST /wallets.</summary>
    public Guid? WalletId { get; init; }

    public required DateTime CreatedAt { get; init; }
}
