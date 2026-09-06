using System.Collections.Concurrent;
using Tugu.Application.Common.Interfaces;
using Tugu.Domain.Entities;

namespace Tugu.Infrastructure.Persistence.InMemory;

/// <summary>TEMPORAL: ver nota en InMemoryUserRepository.</summary>
public class InMemoryDeviceRepository : IDeviceRepository
{
    private readonly ConcurrentDictionary<Guid, Device> _store = new();

    public Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(id, out var device) ? device : null);

    public Task<Device?> GetBySerialAsync(string serialNumber, CancellationToken ct = default) =>
        Task.FromResult(_store.Values.FirstOrDefault(d =>
            string.Equals(d.SerialNumber, serialNumber, StringComparison.OrdinalIgnoreCase)));

    public Task AddAsync(Device device, CancellationToken ct = default)
    {
        _store[device.Id] = device;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Device device, CancellationToken ct = default)
    {
        _store[device.Id] = device;
        return Task.CompletedTask;
    }
}
