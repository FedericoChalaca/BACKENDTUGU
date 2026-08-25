using Tugu.Application.Common.Exceptions;
using Tugu.Application.Common.Interfaces;
using Tugu.Domain.Entities;

namespace Tugu.Application.Devices;

public class DeviceService
{
    private readonly IDeviceRepository _devices;

    public DeviceService(IDeviceRepository devices)
    {
        _devices = devices;
    }

    public async Task<Device> RegisterAsync(string serialNumber, string alias, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            throw new ValidationException("El serial del dispositivo es obligatorio.");

        serialNumber = serialNumber.Trim();

        if (await _devices.GetBySerialAsync(serialNumber, ct) is not null)
            throw new ConflictException("Ya existe un dispositivo registrado con ese serial.");

        var device = new Device
        {
            SerialNumber = serialNumber,
            Alias = string.IsNullOrWhiteSpace(alias) ? serialNumber : alias.Trim(),
            LastSeenAt = DateTime.UtcNow
        };

        await _devices.AddAsync(device, ct);
        return device;
    }
}
