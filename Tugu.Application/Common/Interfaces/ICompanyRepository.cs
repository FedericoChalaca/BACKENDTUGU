using Tugu.Domain.Entities;

namespace Tugu.Application.Common.Interfaces;

public interface ICompanyRepository
{
    /// <summary>Incluye los miembros.</summary>
    Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Company?> GetByNitAsync(string nit, CancellationToken ct = default);

    /// <summary>Comercio al que pertenece el usuario (MVP: un usuario administra un solo comercio).</summary>
    Task<Company?> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default);

    Task AddAsync(Company company, CancellationToken ct = default);

    Task UpdateAsync(Company company, CancellationToken ct = default);

    Task AddMemberAsync(CompanyMember member, CancellationToken ct = default);
}
