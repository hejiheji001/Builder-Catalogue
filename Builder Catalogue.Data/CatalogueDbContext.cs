using BuilderCatalogue.Data.Domain;

namespace BuilderCatalogue.Data;

public class CatalogueDbContext(DbContextOptions<CatalogueDbContext> options) : DbContext(options)
{
    public DbSet<BuildableLEGOSets> BuildableSets => Set<BuildableLEGOSets>();
    public DbSet<LEGOSet> LEGOSets => Set<LEGOSet>();
}
