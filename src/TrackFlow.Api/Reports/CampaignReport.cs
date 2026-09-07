using Microsoft.EntityFrameworkCore;
using TrackFlow.Api.Data;

namespace TrackFlow.Api.Reports;

public record CampaignReportRow(string CampaignId, int Clicks, int Conversions, decimal Revenue, decimal ConversionRate);

public static class CampaignReport
{
    /// <summary>Per-campaign totals for events with OccurredAt in [fromUtc, toUtc).</summary>
    public static async Task<List<CampaignReportRow>> QueryAsync(
        TrackingDbContext db, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var grouped = await db.Events.AsNoTracking()
            .Where(e => e.OccurredAt >= fromUtc && e.OccurredAt < toUtc)
            .GroupBy(e => e.CampaignId)
            .Select(g => new
            {
                CampaignId = g.Key,
                Clicks = g.Count(e => e.Type == EventType.Click),
                Conversions = g.Count(e => e.Type == EventType.Conversion),
                // SQLite has no decimal SUM; pull the amounts and total them client-side.
                Amounts = g.Where(e => e.Type == EventType.Conversion).Select(e => e.Amount).ToList(),
            })
            .OrderBy(x => x.CampaignId)
            .ToListAsync(ct);

        return grouped.Select(x => new CampaignReportRow(
            x.CampaignId, x.Clicks, x.Conversions, x.Amounts.Sum(), Rate(x.Conversions, x.Clicks))).ToList();
    }

    public static decimal Rate(int conversions, int clicks) =>
        clicks == 0 ? 0m : Math.Round((decimal)conversions / clicks, 4);
}
