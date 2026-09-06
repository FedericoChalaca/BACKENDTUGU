namespace Tugu.Contracts.Wallets;

/// <summary>Exactamente uno de userId / companyId. Para companyId, el caller debe ser miembro del comercio.</summary>
public class CreateWalletRequest
{
    public Guid? UserId { get; init; }

    public Guid? CompanyId { get; init; }
}

public class WalletResponse
{
    public required Guid Id { get; init; }

    /// <summary>User o Company.</summary>
    public required string OwnerType { get; init; }

    public Guid? UserId { get; init; }

    public Guid? CompanyId { get; init; }

    public required decimal Balance { get; init; }

    public required string Currency { get; init; }

    public required string Status { get; init; }

    public required DateTime CreatedAt { get; init; }
}
