using BuilderCatalogue.Data.Domain;

namespace BuilderCatalogue.Data;

public class CatalogueDbContext(DbContextOptions<CatalogueDbContext> options) : DbContext(options)
{
    public DbSet<BuildableLEGOSets> BuildableSets => Set<BuildableLEGOSets>();
    public DbSet<LEGOSet> LEGOSets => Set<LEGOSet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LEGOSet>()
            .HasKey(set => new { set.BuildableLEGOSetsId, set.Id });

        modelBuilder.Entity<BuildableLEGOSets>()
            .HasMany(snapshot => snapshot.LEGOSets)
            .WithOne(set => set.BuildableLEGOSets)
            .HasForeignKey(set => set.BuildableLEGOSetsId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
