using Microsoft.EntityFrameworkCore;
using Tugu.Application.Common.Interfaces;
using Tugu.Domain.Entities;

namespace Tugu.Infrastructure.Persistence.Repositories;

public class EfDeviceRepository : IDeviceRepository
{
    private readonly TuguDbContext _db;

    public EfDeviceRepository(TuguDbContext db)
    {
        _db = db;
    }

    public Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Devices.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<Device?> GetBySerialAsync(string serialNumber, CancellationToken ct = default) =>
        _db.Devices.FirstOrDefaultAsync(d => d.SerialNumber.ToLower() == serialNumber.ToLower(), ct);

    public async Task AddAsync(Device device, CancellationToken ct = default)
    {
        _db.Devices.Add(device);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Device device, CancellationToken ct = default)
    {
        _db.Devices.Update(device);
        await _db.SaveChangesAsync(ct);
    }
}
