using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrackFlow.Api.Data;

namespace TrackFlow.Tests;

public class EventEndpointTests
{
    private static object Click(string key = "k1", string campaign = "cmp-1") =>
        new { type = "click", campaignId = campaign, clickId = "clk-1", idempotencyKey = key };

    [Fact]
    public async Task Click_event_is_created()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/events", Click());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("click", body.GetProperty("type").GetString());
        Assert.Equal("cmp-1", body.GetProperty("campaignId").GetString());
        Assert.True(body.GetProperty("id").GetInt64() > 0);
    }

    [Fact]
    public async Task Conversion_event_stores_amount()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/events", new
        {
            type = "conversion", campaignId = "cmp-1", clickId = "clk-1", amount = 12.50m, idempotencyKey = "conv-1"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(12.50m, body.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Duplicate_idempotency_key_returns_original_without_second_row()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var first = await client.PostAsJsonAsync("/v1/events", Click("same"));
        var second = await client.PostAsJsonAsync("/v1/events", Click("same", campaign: "cmp-OTHER"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var a = await first.Content.ReadFromJsonAsync<JsonElement>();
        var b = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(a.GetProperty("id").GetInt64(), b.GetProperty("id").GetInt64());
        Assert.Equal("cmp-1", b.GetProperty("campaignId").GetString());
        // The replayed event must serialize as UTC exactly like the original did.
        Assert.EndsWith("Z", b.GetProperty("occurredAt").GetString());

        using var db = factory.CreateDb();
        Assert.Equal(1, await db.Events.CountAsync());
    }

    [Fact]
    public async Task Concurrent_race_on_one_key_creates_exactly_one_row()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        // The unique index, not the fast-path lookup, is what settles this: fire the same key
        // at the endpoint at the same time and the losers must surface the winner, not err.
        var responses = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => client.PostAsJsonAsync("/v1/events", Click("race-key"))));

        Assert.Contains(HttpStatusCode.Created, responses.Select(r => r.StatusCode));
        Assert.All(responses, r => Assert.True(
            r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"race request failed: {(int)r.StatusCode}"));

        using var db = factory.CreateDb();
        Assert.Equal(1, await db.Events.CountAsync(e => e.IdempotencyKey == "race-key"));
    }

    [Fact]
    public async Task Malformed_json_returns_problem_details_400()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/v1/events",
            new StringContent("not json", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Designatorless_occurredAt_is_treated_as_utc()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/events", new
        {
            type = "click", campaignId = "cmp-1", idempotencyKey = "k-utc",
            occurredAt = "2026-06-01T12:05:00"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Unshifted: the payload's wall clock IS the instant, on any host timezone.
        Assert.Equal("2026-06-01T12:05:00Z", body.GetProperty("occurredAt").GetString());
    }

    [Theory]
    [InlineData("missing campaign", """{"type":"click","idempotencyKey":"k"}""")]
    [InlineData("unknown type", """{"type":"impression","campaignId":"c","idempotencyKey":"k"}""")]
    [InlineData("conversion without clickId", """{"type":"conversion","campaignId":"c","amount":1,"idempotencyKey":"k"}""")]
    [InlineData("negative amount", """{"type":"conversion","campaignId":"c","clickId":"x","amount":-1,"idempotencyKey":"k"}""")]
    [InlineData("missing idempotency key", """{"type":"click","campaignId":"c"}""")]
    public async Task Invalid_event_returns_400_with_problem_details(string scenario, string json)
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/v1/events",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{scenario}: expected 400, got {(int)response.StatusCode}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out var errors));
        Assert.True(errors.EnumerateObject().Any());
    }
}
