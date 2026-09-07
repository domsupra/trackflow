using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TrackFlow.Api.Data;

public class TrackingDbContext : DbContext
{
    public TrackingDbContext(DbContextOptions<TrackingDbContext> options) : base(options) { }

    public DbSet<Event> Events => Set<Event>();
    public DbSet<CampaignHourlyStats> CampaignHourlyStats => Set<CampaignHourlyStats>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Every DateTime in this schema is UTC. SQLite (and some other providers) drop
        // DateTimeKind on read, so pin it back to Utc here rather than in every query.
        var utc = new ValueConverter<DateTime, DateTime>(
            v => v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        modelBuilder.Entity<CampaignHourlyStats>(s =>
        {
            s.HasKey(x => new { x.CampaignId, x.HourUtc });
            s.Property(x => x.CampaignId).HasMaxLength(64);
            s.Property(x => x.Revenue).HasPrecision(18, 4);
            s.Property(x => x.HourUtc).HasConversion(utc);
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.Property(x => x.OccurredAt).HasConversion(utc);
            e.Property(x => x.ReceivedAt).HasConversion(utc);
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
