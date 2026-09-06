using Tugu.Domain.Common;
using Tugu.Domain.Enums;

namespace Tugu.Domain.Entities;

/// <summary>
/// Comercio afiliado (TUGU Negocios). Tiene su propia billetera y uno o más
/// usuarios asociados que lo administran. Los datáfonos pueden pertenecer a
/// un comercio (corresponsal).
/// </summary>
public class Company : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>NIT colombiano, único. Se guarda normalizado (solo dígitos y guion).</summary>
    public string Nit { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public CompanyStatus Status { get; set; } = CompanyStatus.PendingVerification;

    public ICollection<CompanyMember> Members { get; set; } = new List<CompanyMember>();

    public Wallet? Wallet { get; set; }
}

/// <summary>Usuario asociado a un comercio (quien opera TUGU Negocios en su nombre).</summary>
public class CompanyMember : AuditableEntity
{
    public Guid CompanyId { get; set; }

    public Company? Company { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }
}
