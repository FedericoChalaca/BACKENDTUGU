namespace Tugu.Contracts.Users;

public class CreateUserRequest
{
    /// <summary>1=CC, 2=CE, 3=TI, 4=Passport.</summary>
    public required int DocumentType { get; init; }

    public required string DocumentNumber { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string PhoneNumber { get; init; }

    public string? Email { get; init; }
}

/// <summary>
/// Todos los campos son opcionales: solo se actualizan los enviados. El
/// documento de identidad no es editable por API (dato KYC).
/// </summary>
public class UpdateUserRequest
{
    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? PhoneNumber { get; init; }

    public string? Email { get; init; }
}

public class UserResponse
{
    public required Guid Id { get; init; }

    public required string DocumentType { get; init; }

    public required string DocumentNumber { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string PhoneNumber { get; init; }

    public string? Email { get; init; }

    public required string Status { get; init; }

    public required DateTime CreatedAt { get; init; }
}
