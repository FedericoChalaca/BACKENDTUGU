using Tugu.Application.Common.Exceptions;
using Tugu.Application.Devices;
using Tugu.Domain.Enums;
using Tugu.Infrastructure.Persistence.InMemory;

namespace Tugu.Tests.Application;

public class DeviceServiceTests
{
    private static DeviceService NewService() => new(new InMemoryDeviceRepository(), new InMemoryCompanyRepository());

    [Fact]
    public async Task RegisterAsync_SerialNuevo_RegistraActivo()
    {
        var service = NewService();

        var device = await service.RegisterAsync("SN-001", "Datáfono tienda Belén");

        Assert.Equal("SN-001", device.SerialNumber);
        Assert.Equal(DeviceStatus.Active, device.Status);
        Assert.NotNull(device.LastSeenAt);
    }

    [Fact]
    public async Task RegisterAsync_SerialDuplicado_LanzaConflict()
    {
        var service = NewService();
        await service.RegisterAsync("SN-001", "Uno");

        await Assert.ThrowsAsync<ConflictException>(() => service.RegisterAsync("sn-001", "Otro"));
    }

    [Fact]
    public async Task RegisterAsync_SinSerial_LanzaValidation()
    {
        var service = NewService();

        await Assert.ThrowsAsync<ValidationException>(() => service.RegisterAsync("  ", "Alias"));
    }
}
