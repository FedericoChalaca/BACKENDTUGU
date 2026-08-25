namespace Tugu.Contracts.Wallets;

public class CreateWalletRequest
{
    public required Guid UserId { get; init; }
}

public class WalletResponse
{
    public required Guid Id { get; init; }

    public required Guid UserId { get; init; }

    public required decimal Balance { get; init; }

    public required string Currency { get; init; }

    public required string Status { get; init; }

    public required DateTime CreatedAt { get; init; }
}
