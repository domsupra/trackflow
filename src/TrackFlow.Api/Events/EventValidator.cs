using TrackFlow.Api.Data;

namespace TrackFlow.Api.Events;

/// <summary>
/// Validates an <see cref="EventRequest"/> and, when valid, produces the <see cref="Event"/> to store.
/// Returns errors keyed by field so the endpoint can hand them straight to ProblemDetails.
/// </summary>
public static class EventValidator
{
    /// <summary>
    /// Campaign and click identifiers are echoed back to callers and rendered in the
    /// report table, so they are restricted to identifier characters rather than any
    /// text within the length limit — a public write endpoint should not let a caller
    /// choose arbitrary prose that later appears on someone else's screen.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex IdentifierPattern =
        new(@"^[A-Za-z0-9._-]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Normalises client timestamps: a value without a timezone designator (no
    /// <c>DateTimeOffset</c>) is interpreted as UTC, not as the server's local time, so
    /// the same payload means the same instant on every host.
    /// </summary>
    public static DateTime ToUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    public static bool TryParse(EventRequest request, DateTime nowUtc, out Event entity, out Dictionary<string, string[]> errors)
    {
        var errs = new Dictionary<string, List<string>>();
        void Add(string field, string message)
        {
            if (!errs.TryGetValue(field, out var list)) errs[field] = list = new List<string>();
            list.Add(message);
        }

        EventType type = default;
        if (string.IsNullOrWhiteSpace(request.Type))
            Add("type", "Required.");
        else if (!Enum.TryParse(request.Type, ignoreCase: true, out type) || !Enum.IsDefined(type))
            Add("type", "Must be 'click' or 'conversion'.");

        if (string.IsNullOrWhiteSpace(request.CampaignId))
            Add("campaignId", "Required.");
        else if (request.CampaignId.Length > 64)
            Add("campaignId", "Must be 64 characters or fewer.");
        else if (!IdentifierPattern.IsMatch(request.CampaignId.Trim()))
            Add("campaignId", "Must contain only letters, digits, dot, underscore or hyphen.");

        if (request.ClickId is { Length: > 128 })
            Add("clickId", "Must be 128 characters or fewer.");
        else if (!string.IsNullOrWhiteSpace(request.ClickId) && !IdentifierPattern.IsMatch(request.ClickId.Trim()))
            Add("clickId", "Must contain only letters, digits, dot, underscore or hyphen.");

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            Add("idempotencyKey", "Required.");
        else if (request.IdempotencyKey.Length > 128)
            Add("idempotencyKey", "Must be 128 characters or fewer.");

        if (type == EventType.Conversion)
        {
            if (string.IsNullOrWhiteSpace(request.ClickId))
                Add("clickId", "Required for conversion events.");
            if (request.Amount is < 0)
                Add("amount", "Must be zero or greater.");
        }
        else if (request.Amount is not null and not 0)
        {
            Add("amount", "Only valid on conversion events.");
        }

        errors = errs.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
        if (errors.Count > 0)
        {
            entity = null!;
            return false;
        }

        entity = new Event
        {
            Type = type,
            CampaignId = request.CampaignId!.Trim(),
            ClickId = string.IsNullOrWhiteSpace(request.ClickId) ? null : request.ClickId.Trim(),
            Amount = type == EventType.Conversion ? request.Amount ?? 0m : 0m,
            OccurredAt = ToUtc(request.OccurredAt ?? nowUtc),
            IdempotencyKey = request.IdempotencyKey!.Trim(),
            ReceivedAt = nowUtc,
        };
        return true;
    }
}
