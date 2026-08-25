using Tugu.Domain.Common;
using Tugu.Domain.Enums;

namespace Tugu.Domain.Entities;

/// <summary>
/// Billetera de un usuario. Cuando TUGU Negocios entre al alcance se agregará
/// el dueño tipo comercio (CompanyId nullable); por ahora siempre es un usuario.
/// </summary>
public class Wallet : AuditableEntity
{
    public Guid UserId { get; set; }

    public User? User { get; set; }

    /// <summary>Por ahora siempre User; Company se habilita cuando entre TUGU Negocios.</summary>
    public WalletOwnerType OwnerType { get; set; } = WalletOwnerType.User;

    /// <summary>
    /// Siempre decimal, nunca float/double. Solo lo modifica el motor
    /// transaccional dentro de una transacción SQL atómica.
    /// </summary>
    public decimal Balance { get; set; }

    public string Currency { get; set; } = "COP";

    public WalletStatus Status { get; set; } = WalletStatus.Active;

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
