using Tugu.Domain.Common;
using Tugu.Domain.Enums;

namespace Tugu.Domain.Entities;

/// <summary>
/// Usuario final (TUGU Personal). En el punto de venta se identifica SOLO con
/// su huella (identificación 1:N). El documento no se usa para pagar: es dato
/// de identidad/KYC obligatorio en un producto financiero.
/// Las credenciales de login viven en Cognito (fase posterior), no aquí.
/// </summary>
public class User : AuditableEntity
{
    public DocumentType DocumentType { get; set; }

    /// <summary>Único junto con DocumentType (índice único en BD).</summary>
    public string DocumentNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>Único. Futuro login/OTP con Cognito.</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    public string? Email { get; set; }

    public UserStatus Status { get; set; } = UserStatus.PendingVerification;

    public Wallet? Wallet { get; set; }

    public Biometric? Biometric { get; set; }
}
