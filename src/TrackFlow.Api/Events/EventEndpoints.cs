using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TrackFlow.Api.Data;

namespace TrackFlow.Api.Events;

public static class EventEndpoints
{
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/events", CreateEvent);
        return app;
    }

    private static async Task<IResult> CreateEvent(HttpRequest request, TrackingDbContext db, CancellationToken ct)
    {
        // Parse the body ourselves so a malformed payload becomes a validation problem
        // (400 ProblemDetails) instead of an uncaught binding exception (500).
        EventRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<EventRequest>();
        }
        catch (JsonException)
        {
            body = null;
        }

        if (body is null)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["body"] = new[] { "Request body must be a valid JSON object." }
            });

        if (!EventValidator.TryParse(body, DateTime.UtcNow, out var entity, out var errors))
            return Results.ValidationProblem(errors);

        // Fast path: already seen this key, return what we stored the first time.
        var existing = await db.Events.AsNoTracking()
            .FirstOrDefaultAsync(e => e.IdempotencyKey == entity.IdempotencyKey, ct);
        if (existing is not null)
            return Results.Ok(ToResponse(existing));

        db.Events.Add(entity);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race with a concurrent request carrying the same key; the unique index
            // rejected our row. Return the winner instead of a 500.
            db.Entry(entity).State = EntityState.Detached;
            var winner = await db.Events.AsNoTracking()
                .FirstAsync(e => e.IdempotencyKey == entity.IdempotencyKey, ct);
            return Results.Ok(ToResponse(winner));
        }

        return Results.Created($"/v1/events/{entity.Id}", ToResponse(entity));
    }

    private static EventResponse ToResponse(Event e) =>
        new(e.Id, e.Type.ToString().ToLowerInvariant(), e.CampaignId, e.ClickId, e.Amount, e.OccurredAt);
}
