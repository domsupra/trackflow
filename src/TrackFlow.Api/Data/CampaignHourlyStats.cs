namespace TrackFlow.Api.Data;

/// <summary>Pre-aggregated totals for one campaign in one UTC hour. Written by the rollup job.</summary>
public class CampaignHourlyStats
{
    public string CampaignId { get; set; } = "";
    public DateTime HourUtc { get; set; }
    public int Clicks { get; set; }
    public int Conversions { get; set; }
    public decimal Revenue { get; set; }
}
