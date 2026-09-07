using TrackFlow.Api.Data;

namespace TrackFlow.Api.Events;

/// <summary>
/// Validates an <see cref="EventRequest"/> and, when valid, produces the <see cref="Event"/> to store.
/// Returns errors keyed by field so the endpoint can hand them straight to ProblemDetails.
/// </summary>
public static class EventValidator
{
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

        if (request.ClickId is { Length: > 128 })
            Add("clickId", "Must be 128 characters or fewer.");

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
            OccurredAt = (request.OccurredAt ?? nowUtc).ToUniversalTime(),
            IdempotencyKey = request.IdempotencyKey!.Trim(),
            ReceivedAt = nowUtc,
        };
        return true;
    }
}
