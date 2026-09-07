using Microsoft.EntityFrameworkCore;

namespace TrackFlow.Api.Data;

public class TrackingDbContext : DbContext
{
    public TrackingDbContext(DbContextOptions<TrackingDbContext> options) : base(options) { }

    public DbSet<Event> Events => Set<Event>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(e =>
        {
            e.Property(x => x.CampaignId).HasMaxLength(64).IsRequired();
            e.Property(x => x.ClickId).HasMaxLength(128);
            e.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
            e.Property(x => x.Amount).HasPrecision(18, 4);
            // Dedupe is enforced by the database, not just by the code path.
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
            e.HasIndex(x => new { x.CampaignId, x.OccurredAt });
        });
    }
}
