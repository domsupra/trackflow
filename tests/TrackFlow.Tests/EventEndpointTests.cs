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

        using var db = factory.CreateDb();
        Assert.Equal(1, await db.Events.CountAsync());
    }

    [Theory]
    [InlineData("missing campaign", """{"type":"click","idempotencyKey":"k"}""")]
    [InlineData("unknown type", """{"type":"impression","campaignId":"c","idempotencyKey":"k"}""")]
    [InlineData("conversion without clickId", """{"type":"conversion","campaignId":"c","amount":1,"idempotencyKey":"k"}""")]
    [InlineData("negative amount", """{"type":"conversion","campaignId":"c","clickId":"x","amount":-1,"idempotencyKey":"k"}""")]
    [InlineData("missing idempotency key", """{"type":"click","campaignId":"c"}""")]
    public async Task Invalid_event_returns_400_with_problem_details(string _, string json)
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/v1/events",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out var errors));
        Assert.True(errors.EnumerateObject().Any());
    }
}
