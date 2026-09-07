namespace TrackFlow.Api.Events;

/// <summary>Inbound event payload. Kept as a plain record so validation is explicit and testable.</summary>
public record EventRequest(
    string? Type,
    string? CampaignId,
    string? ClickId,
    decimal? Amount,
    DateTime? OccurredAt,
    string? IdempotencyKey);

public record EventResponse(
    long Id,
    string Type,
    string CampaignId,
    string? ClickId,
    decimal Amount,
    DateTime OccurredAt);
