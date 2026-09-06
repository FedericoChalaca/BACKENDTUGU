using Tugu.Application.Common.Exceptions;
using Tugu.Application.Devices;
using Tugu.Domain.Enums;
using Tugu.Infrastructure.Persistence.InMemory;

namespace Tugu.Tests.Application;

public class DeviceServiceLifecycleTests
{
    private readonly DeviceService _service = new(new InMemoryDeviceRepository(), new InMemoryCompanyRepository());

    [Fact]
    public async Task GetByIdAsync_Inexistente_LanzaNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task HeartbeatAsync_ActualizaUltimaConexion()
    {
        var device = await _service.RegisterAsync("SN-HB", "Datáfono");
        var before = device.LastSeenAt!.Value;
        await Task.Delay(5);

        var after = await _service.HeartbeatAsync(device.Id);

        Assert.True(after.LastSeenAt > before);
    }

    [Fact]
    public async Task DeactivateAsync_LuegoActivateAsync_CambiaElEstado()
    {
        var device = await _service.RegisterAsync("SN-ST", "Datáfono");
        Assert.Equal(DeviceStatus.Active, device.Status);

        var inactive = await _service.DeactivateAsync(device.Id);
        Assert.Equal(DeviceStatus.Inactive, inactive.Status);

        var active = await _service.ActivateAsync(device.Id);
        Assert.Equal(DeviceStatus.Active, active.Status);
    }

    [Fact]
    public async Task ActivateAsync_YaActivo_LanzaConflict()
    {
        var device = await _service.RegisterAsync("SN-DUP", "Datáfono");

        await Assert.ThrowsAsync<ConflictException>(() => _service.ActivateAsync(device.Id));
    }

    [Fact]
    public async Task DeactivateAsync_Inexistente_LanzaNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeactivateAsync(Guid.NewGuid()));
    }
}
