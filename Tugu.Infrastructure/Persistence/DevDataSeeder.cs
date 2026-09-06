using Microsoft.EntityFrameworkCore;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

namespace Tugu.Infrastructure.Persistence;

/// <summary>
/// Seed de desarrollo, idempotente por entidad: aplica migraciones pendientes
/// y crea lo que falte (usuarios de prueba, comercio de prueba con billetera y
/// datáfono). Solo se invoca en Development.
/// </summary>
public static class DevDataSeeder
{
    public static async Task MigrateAndSeedAsync(TuguDbContext db, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        var ana = await EnsureUserAsync(db, "1017000001", "Ana", "3000000001", 50000m, ct);
        var carlos = await EnsureUserAsync(db, "1017000002", "Carlos", "3000000002", 0m, ct);

        await EnsureCompanyAsync(db, carlos, ct);
    }

    private static async Task<User> EnsureUserAsync(
        TuguDbContext db, string document, string firstName, string phone, decimal initialBalance, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(
            u => u.DocumentType == DocumentType.CC && u.DocumentNumber == document, ct);

        if (user is not null)
            return user;

        user = new User
        {
            DocumentType = DocumentType.CC,
            DocumentNumber = document,
            FirstName = firstName,
            LastName = "Prueba",
            PhoneNumber = phone,
            Email = $"{firstName.ToLowerInvariant()}.prueba@tugu.dev",
            Status = UserStatus.Active,
            CreatedBy = "seed"
        };

        db.Users.Add(user);
        db.Wallets.Add(new Wallet
        {
            UserId = user.Id,
            OwnerType = WalletOwnerType.User,
            Balance = initialBalance,
            Currency = "COP",
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync(ct);
        return user;
    }

    private static async Task EnsureCompanyAsync(TuguDbContext db, User admin, CancellationToken ct)
    {
        const string nit = "900123456-7";

        if (await db.Companies.AnyAsync(c => c.Nit == nit, ct))
            return;

        // Un usuario administra un solo comercio: si Carlos ya tiene otro, no forzar.
        if (await db.CompanyMembers.AnyAsync(m => m.UserId == admin.Id, ct))
            return;

        var tienda = new Company
        {
            Name = "Tienda Prueba",
            Nit = nit,
            Email = "tienda.prueba@tugu.dev",
            PhoneNumber = "6040000001",
            Status = CompanyStatus.Active,
            CreatedBy = "seed"
        };
        tienda.Members.Add(new CompanyMember { CompanyId = tienda.Id, UserId = admin.Id, CreatedBy = "seed" });

        db.Companies.Add(tienda);
        db.Wallets.Add(new Wallet
        {
            CompanyId = tienda.Id,
            OwnerType = WalletOwnerType.Company,
            Balance = 0m,
            Currency = "COP",
            CreatedBy = "seed"
        });

        if (!await db.Devices.AnyAsync(d => d.SerialNumber == "SN-SEED-0001", ct))
        {
            db.Devices.Add(new Device
            {
                SerialNumber = "SN-SEED-0001",
                Alias = "Datáfono Tienda Prueba",
                Status = DeviceStatus.Active,
                CompanyId = tienda.Id,
                LastSeenAt = DateTime.UtcNow,
                CreatedBy = "seed"
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
