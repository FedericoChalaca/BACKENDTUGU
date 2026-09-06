using System.Collections.Concurrent;
using Tugu.Application.Common.Interfaces;
using Tugu.Domain.Entities;

namespace Tugu.Infrastructure.Persistence.InMemory;

/// <summary>Solo para tests unitarios.</summary>
public class InMemoryCompanyRepository : ICompanyRepository
{
    private readonly ConcurrentDictionary<Guid, Company> _store = new();

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(id, out var c) ? c : null);

    public Task<Company?> GetByNitAsync(string nit, CancellationToken ct = default) =>
        Task.FromResult(_store.Values.FirstOrDefault(c => c.Nit == nit));

    public Task<Company?> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(_store.Values.FirstOrDefault(c => c.Members.Any(m => m.UserId == userId)));

    public Task AddAsync(Company company, CancellationToken ct = default)
    {
        _store[company.Id] = company;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Company company, CancellationToken ct = default)
    {
        _store[company.Id] = company;
        return Task.CompletedTask;
    }

    /// <summary>
    /// No-op: el objeto Company en memoria es el mismo que manipula el servicio,
    /// que es quien agrega el miembro a la colección (igual que hace EF vía fixup).
    /// </summary>
    public Task AddMemberAsync(CompanyMember member, CancellationToken ct = default) => Task.CompletedTask;
}
