using System.Collections.Concurrent;
using Tugu.Application.Common.Interfaces;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

namespace Tugu.Infrastructure.Persistence.InMemory;

/// <summary>
/// TEMPORAL: persistencia en memoria para desarrollar sin base de datos.
/// Se reemplaza por EF Core + PostgreSQL en la Tarea 1.3. Los datos se
/// pierden al reiniciar la API.
/// </summary>
public class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, User> _store = new();

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(id, out var user) ? user : null);

    public Task<User?> GetByDocumentAsync(DocumentType documentType, string documentNumber, CancellationToken ct = default) =>
        Task.FromResult(_store.Values.FirstOrDefault(u =>
            u.DocumentType == documentType &&
            string.Equals(u.DocumentNumber, documentNumber, StringComparison.OrdinalIgnoreCase)));

    public Task<User?> GetByPhoneAsync(string phoneNumber, CancellationToken ct = default) =>
        Task.FromResult(_store.Values.FirstOrDefault(u => u.PhoneNumber == phoneNumber));

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        _store[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _store[user.Id] = user;
        return Task.CompletedTask;
    }
}
