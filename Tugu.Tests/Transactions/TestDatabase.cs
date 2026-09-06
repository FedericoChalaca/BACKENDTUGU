using Microsoft.EntityFrameworkCore;
using Tugu.Infrastructure.Persistence;

namespace Tugu.Tests.Transactions;

/// <summary>
/// Aplica las migraciones UNA sola vez por proceso de tests. xUnit corre las
/// clases de integración en paralelo; sin este candado, dos clases pueden
/// intentar migrar a la vez sobre una base recién creada y una de ellas falla
/// (y se marca como omitida en vez de probar).
/// </summary>
public static class TestDatabase
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _migrated;

    public static async Task EnsureMigratedAsync(TuguDbContext db)
    {
        if (_migrated) return;

        await Gate.WaitAsync();
        try
        {
            if (!_migrated)
            {
                await db.Database.MigrateAsync();
                _migrated = true;
            }
        }
        finally
        {
            Gate.Release();
        }
    }
}
