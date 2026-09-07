using Microsoft.EntityFrameworkCore;
using TrackFlow.Api.Data;

namespace TrackFlow.Api.Rollup;

/// <summary>
/// Aggregates raw events into <see cref="CampaignHourlyStats"/>, one row per campaign per UTC hour.
/// Only complete hours (hour end &lt;= upToUtc) are rolled up. Each run recomputes every hour that
/// has raw events newer than the last rollup watermark and upserts the totals, so running it twice,
/// or after late-arriving events, converges on the same numbers instead of double counting.
/// </summary>
public class RollupService
{
    private readonly TrackingDbContext _db;
    public RollupService(TrackingDbContext db) => _db = db;

    public async Task RollupAsync(DateTime upToUtc, CancellationToken ct)
    {
        var cutoff = TruncateToHour(upToUtc);       // hours strictly before this are complete
        var lastRolled = await _db.CampaignHourlyStats.MaxAsync(s => (DateTime?)s.HourUtc, ct);
        var from = lastRolled ?? DateTime.MinValue;   // re-include the last hour in case of late events

        var totals = await _db.Events.AsNoTracking()
            .Where(e => e.OccurredAt >= from && e.OccurredAt < cutoff)
            .Select(e => new { e.CampaignId, e.OccurredAt, e.Type, e.Amount })
            .ToListAsync(ct);

        var byHour = totals
            .GroupBy(e => (e.CampaignId, Hour: TruncateToHour(e.OccurredAt)))
            .Select(g => new CampaignHourlyStats
            {
                CampaignId = g.Key.CampaignId,
                HourUtc = g.Key.Hour,
                Clicks = g.Count(e => e.Type == EventType.Click),
                Conversions = g.Count(e => e.Type == EventType.Conversion),
                Revenue = g.Where(e => e.Type == EventType.Conversion).Sum(e => e.Amount),
            });

        foreach (var stat in byHour)
        {
            var existing = await _db.CampaignHourlyStats.FindAsync(new object[] { stat.CampaignId, stat.HourUtc }, ct);
            if (existing is null)
                _db.CampaignHourlyStats.Add(stat);
            else
                (existing.Clicks, existing.Conversions, existing.Revenue) = (stat.Clicks, stat.Conversions, stat.Revenue);
        }

        await _db.SaveChangesAsync(ct);
    }

    private static DateTime TruncateToHour(DateTime utc) =>
        new(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);
}
