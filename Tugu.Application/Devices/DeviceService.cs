using Tugu.Application.Common.Exceptions;
using Tugu.Application.Common.Interfaces;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

namespace Tugu.Application.Devices;

public class DeviceService
{
    private readonly IDeviceRepository _devices;

    public DeviceService(IDeviceRepository devices)
    {
        _devices = devices;
    }

    public async Task<Device> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _devices.GetByIdAsync(id, ct)
               ?? throw new NotFoundException($"No existe un dispositivo con id {id}.");
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

    /// <summary>
    /// Señal de vida del datáfono. Solo actualiza LastSeenAt; un dispositivo
    /// inactivo puede reportarse (para saber que existe) pero no operar.
    /// </summary>
    public async Task<Device> HeartbeatAsync(Guid id, CancellationToken ct = default)
    {
        var device = await GetByIdAsync(id, ct);
        device.LastSeenAt = DateTime.UtcNow;
        device.UpdatedAt = DateTime.UtcNow;
        await _devices.UpdateAsync(device, ct);
        return device;
    }

    public Task<Device> ActivateAsync(Guid id, CancellationToken ct = default) =>
        SetStatusAsync(id, DeviceStatus.Active, ct);

    public Task<Device> DeactivateAsync(Guid id, CancellationToken ct = default) =>
        SetStatusAsync(id, DeviceStatus.Inactive, ct);

    private async Task<Device> SetStatusAsync(Guid id, DeviceStatus status, CancellationToken ct)
    {
        var device = await GetByIdAsync(id, ct);

        if (device.Status == status)
            throw new ConflictException($"El dispositivo ya está en estado {status}.");

        device.Status = status;
        device.UpdatedAt = DateTime.UtcNow;
        await _devices.UpdateAsync(device, ct);
        return device;
    }
}
