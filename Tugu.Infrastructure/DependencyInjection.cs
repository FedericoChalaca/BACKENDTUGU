using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tugu.Application.Common.Interfaces;
using Tugu.Infrastructure.Persistence;
using Tugu.Infrastructure.Persistence.Repositories;

namespace Tugu.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra la persistencia real: PostgreSQL vía EF Core.
    /// Los repositorios en memoria quedan solo para tests unitarios.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TuguDb")
            ?? throw new InvalidOperationException(
                "Falta la cadena de conexión 'TuguDb' en appsettings.");

        services.AddDbContext<TuguDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IWalletRepository, EfWalletRepository>();
        services.AddScoped<IDeviceRepository, EfDeviceRepository>();
        services.AddScoped<Application.Transactions.ITransactionEngine, Transactions.TransactionEngine>();

        return services;
    }
}
