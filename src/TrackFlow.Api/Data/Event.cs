namespace TrackFlow.Api.Data;

public enum EventType
{
    Click = 1,
    Conversion = 2,
}

/// <summary>A single raw tracking event. Immutable once written.</summary>
public class Event
{
    public long Id { get; set; }
    public EventType Type { get; set; }
    public string CampaignId { get; set; } = "";
    public string? ClickId { get; set; }
    public decimal Amount { get; set; }
    public DateTime OccurredAt { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
}
