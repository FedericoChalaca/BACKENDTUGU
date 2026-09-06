using Tugu.Domain.Entities;

namespace Tugu.Application.Common.Interfaces;

public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Device?> GetBySerialAsync(string serialNumber, CancellationToken ct = default);

    Task AddAsync(Device device, CancellationToken ct = default);

    Task UpdateAsync(Device device, CancellationToken ct = default);
}
