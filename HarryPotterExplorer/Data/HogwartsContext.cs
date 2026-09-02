using Microsoft.EntityFrameworkCore;

namespace HarryPotterExplorer.Data;

public class HogwartsContext(DbContextOptions<HogwartsContext> options) : DbContext(options)
{
    public DbSet<CharacterEntity> Characters => Set<CharacterEntity>();
    public DbSet<SpellEntity> Spells => Set<SpellEntity>();
    public DbSet<ArtifactEntity> Artifacts => Set<ArtifactEntity>();
    public DbSet<CharacterStatEntity> CharacterStats => Set<CharacterStatEntity>();
    public DbSet<LedgerEventEntity> LedgerEvents => Set<LedgerEventEntity>();
    public DbSet<SyncRunEntity> SyncRuns => Set<SyncRunEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CharacterEntity>(e =>
        {
            e.HasIndex(c => c.House);
            e.HasIndex(c => c.Name);
            e.HasIndex(c => c.SearchIndex);
        });

        modelBuilder.Entity<SpellEntity>(e => e.HasIndex(s => s.SearchIndex));
        modelBuilder.Entity<ArtifactEntity>(e => e.HasIndex(a => a.Category));

        modelBuilder.Entity<LedgerEventEntity>(e =>
        {
            e.HasIndex(l => l.CreatedUtc);
            e.HasIndex(l => new { l.VisitorId, l.CharacterId });
        });

        modelBuilder.Entity<CharacterStatEntity>(e => e.HasIndex(s => s.CollectCount));
    }
}
