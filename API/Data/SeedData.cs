using Microsoft.EntityFrameworkCore;

namespace API.Data;

public static class SeedData
{
    public static async Task SeedAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        // Only seed if table is empty (idempotent)
        if (await db.PowerPlants.AnyAsync(ct)) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var seeds = new[]
        {
            new Models.PowerPlant
            {
                Id = Guid.NewGuid(),
                Power = 150.0m,
                Owner = "Acme Energy",
                ValidFrom = today,
                ValidTo = null
            },
            new Models.PowerPlant
            {
                Id = Guid.NewGuid(),
                Power = 275.5m,
                Owner = "NorthGrid",
                ValidFrom = today,
                ValidTo = null
            }
        };

        db.PowerPlants.AddRange(seeds);
        await db.SaveChangesAsync(ct);
    }
}