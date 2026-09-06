using Microsoft.Extensions.DependencyInjection;
using Tugu.Application.Biometrics;
using Tugu.Application.Devices;
using Tugu.Application.Users;
using Tugu.Application.Wallets;

namespace Tugu.Application;

public static class DependencyInjection
{
    /// <summary>Registra los servicios de la capa Application (casos de uso).</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<UserService>();
        services.AddScoped<WalletService>();
        services.AddScoped<DeviceService>();
        services.AddScoped<BiometricService>();
        services.AddScoped<Transactions.TransactionQueryService>();
        return services;
    }
}
