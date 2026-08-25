using Microsoft.EntityFrameworkCore;
using Tugu.Domain.Entities;
using Tugu.Domain.Enums;

namespace Tugu.Infrastructure.Persistence;

/// <summary>
/// Seed mínimo de desarrollo: aplica migraciones pendientes y crea 2 usuarios
/// de prueba con billetera si la base está vacía. Solo se invoca en Development.
/// </summary>
public static class DevDataSeeder
{
    public static async Task MigrateAndSeedAsync(TuguDbContext db, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        if (await db.Users.AnyAsync(ct))
            return;

        var ana = new User
        {
            DocumentType = DocumentType.CC,
            DocumentNumber = "1017000001",
            FirstName = "Ana",
            LastName = "Prueba",
            PhoneNumber = "3000000001",
            Email = "ana.prueba@tugu.dev",
            Status = UserStatus.Active,
            CreatedBy = "seed"
        };

        var carlos = new User
        {
            DocumentType = DocumentType.CC,
            DocumentNumber = "1017000002",
            FirstName = "Carlos",
            LastName = "Prueba",
            PhoneNumber = "3000000002",
            Email = "carlos.prueba@tugu.dev",
            Status = UserStatus.Active,
            CreatedBy = "seed"
        };

        db.Users.AddRange(ana, carlos);
        db.Wallets.AddRange(
            new Wallet { UserId = ana.Id, Balance = 50000m, Currency = "COP", CreatedBy = "seed" },
            new Wallet { UserId = carlos.Id, Balance = 0m, Currency = "COP", CreatedBy = "seed" });

        await db.SaveChangesAsync(ct);
    }
}
