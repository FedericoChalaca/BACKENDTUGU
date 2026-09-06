using Tugu.Application.Common.Exceptions;
using Tugu.Application.Common.Interfaces;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

namespace Tugu.Application.Devices;

public class DeviceService
{
    private readonly IDeviceRepository _devices;
    private readonly ICompanyRepository _companies;

    public DeviceService(IDeviceRepository devices, ICompanyRepository companies)
    {
        _devices = devices;
        _companies = companies;
    }

    public async Task<Device> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _devices.GetByIdAsync(id, ct)
               ?? throw new NotFoundException($"No existe un dispositivo con id {id}.");
    }

    /// <summary>Registra un datáfono; opcionalmente asignado a un comercio (corresponsal).</summary>
    public async Task<Device> RegisterAsync(
        string serialNumber, string alias, Guid? companyId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            throw new ValidationException("El serial del dispositivo es obligatorio.");

        serialNumber = serialNumber.Trim();

        if (await _devices.GetBySerialAsync(serialNumber, ct) is not null)
            throw new ConflictException("Ya existe un dispositivo registrado con ese serial.");

        if (companyId is Guid cid && await _companies.GetByIdAsync(cid, ct) is null)
            throw new NotFoundException($"No existe un comercio con id {cid}.");

        var device = new Device
        {
            SerialNumber = serialNumber,
            Alias = string.IsNullOrWhiteSpace(alias) ? serialNumber : alias.Trim(),
            CompanyId = companyId,
            LastSeenAt = DateTime.UtcNow
        };

        await _devices.AddAsync(device, ct);
        return device;
    }

    /// <summary>Asigna (o reasigna) el datáfono a un comercio.</summary>
    public async Task<Device> AssignToCompanyAsync(Guid id, Guid companyId, CancellationToken ct = default)
    {
        var device = await GetByIdAsync(id, ct);

        if (await _companies.GetByIdAsync(companyId, ct) is null)
            throw new NotFoundException($"No existe un comercio con id {companyId}.");

        device.CompanyId = companyId;
        device.UpdatedAt = DateTime.UtcNow;
        await _devices.UpdateAsync(device, ct);
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
