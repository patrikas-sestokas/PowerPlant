using Microsoft.EntityFrameworkCore;

namespace API;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Models.PowerPlant> PowerPlants { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("unaccent");
        var mi = typeof(PgFunctions).GetMethod(nameof(PgFunctions.Unaccent), [typeof(string)]);
        if (mi != null)
            modelBuilder
                .HasDbFunction(mi)
                .HasName("unaccent")
                .HasSchema(null);
        base.OnModelCreating(modelBuilder);
    }
}

public static class PgFunctions
{
    [DbFunction("unaccent")]
    public static string Unaccent(string value) => value;
}
