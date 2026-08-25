using Tugu.Application.Common.Exceptions;
using Tugu.Application.Common.Interfaces;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

namespace Tugu.Application.Users;

public class UserService
{
    private readonly IUserRepository _users;

    public UserService(IUserRepository users)
    {
        _users = users;
    }

    public async Task<User> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _users.GetByIdAsync(id, ct)
               ?? throw new NotFoundException($"No existe un usuario con id {id}.");
    }

    public async Task<User> CreateAsync(
        DocumentType documentType,
        string documentNumber,
        string firstName,
        string lastName,
        string phoneNumber,
        string? email,
        CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(documentNumber))
            errors["documentNumber"] = new[] { "El número de documento es obligatorio." };
        if (string.IsNullOrWhiteSpace(firstName))
            errors["firstName"] = new[] { "El nombre es obligatorio." };
        if (string.IsNullOrWhiteSpace(lastName))
            errors["lastName"] = new[] { "El apellido es obligatorio." };
        if (string.IsNullOrWhiteSpace(phoneNumber))
            errors["phoneNumber"] = new[] { "El teléfono es obligatorio." };
        if (!string.IsNullOrWhiteSpace(email) && !email.Contains('@'))
            errors["email"] = new[] { "El email no tiene un formato válido." };

        if (errors.Count > 0)
            throw new ValidationException("Datos de usuario inválidos.", errors);

        documentNumber = documentNumber.Trim();
        phoneNumber = phoneNumber.Trim();

        if (await _users.GetByDocumentAsync(documentType, documentNumber, ct) is not null)
            throw new ConflictException("Ya existe un usuario con ese documento.");

        if (await _users.GetByPhoneAsync(phoneNumber, ct) is not null)
            throw new ConflictException("Ya existe un usuario con ese teléfono.");

        var user = new User
        {
            DocumentType = documentType,
            DocumentNumber = documentNumber,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            PhoneNumber = phoneNumber,
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            Status = UserStatus.PendingVerification
        };

        await _users.AddAsync(user, ct);
        return user;
    }
}
