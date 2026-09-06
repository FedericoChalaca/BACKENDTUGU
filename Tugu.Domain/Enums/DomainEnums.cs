namespace Tugu.Domain.Enums;

/// <summary>Tipos de documento de identidad usados en Colombia.</summary>
public enum DocumentType
{
    CC = 1,        // Cédula de ciudadanía
    CE = 2,        // Cédula de extranjería
    TI = 3,        // Tarjeta de identidad
    Passport = 4
}

public enum UserStatus
{
    PendingVerification = 1,
    Active = 2,
    Blocked = 3
}

public enum WalletOwnerType
{
    User = 1,
    Company = 2
}

public enum CompanyStatus
{
    PendingVerification = 1,
    Active = 2,
    Blocked = 3
}

public enum WalletStatus
{
    Active = 1,
    Frozen = 2,
    Closed = 3
}

public enum TransactionType
{
    Recharge = 1,
    Withdrawal = 2
}

public enum TransactionStatus
{
    Pending = 1,
    Completed = 2,
    Failed = 3,
    Reversed = 4
}

public enum BiometricStatus
{
    Active = 1,
    Revoked = 2
}

public enum DeviceStatus
{
    Active = 1,
    Inactive = 2
}
