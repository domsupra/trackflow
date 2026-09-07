using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TrackFlow.Api.Data;

namespace TrackFlow.Tests;

public class CampaignReportTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static async Task Seed(ApiFactory factory, params Event[] events)
    {
        using var db = factory.CreateDb();
        db.Events.AddRange(events);
        await db.SaveChangesAsync();
    }

    private static Event Ev(EventType type, string campaign, DateTime at, decimal amount = 0, string? key = null) => new()
    {
        Type = type, CampaignId = campaign, ClickId = "c", Amount = amount,
        OccurredAt = at, IdempotencyKey = key ?? Guid.NewGuid().ToString(), ReceivedAt = at,
    };

    [Fact]
    public async Task Report_aggregates_clicks_conversions_revenue_and_rate_per_campaign()
    {
        using var factory = new ApiFactory();
        await Seed(factory,
            Ev(EventType.Click, "A", T0.AddMinutes(1)),
            Ev(EventType.Click, "A", T0.AddMinutes(2)),
            Ev(EventType.Click, "A", T0.AddMinutes(3)),
            Ev(EventType.Click, "A", T0.AddMinutes(4)),
            Ev(EventType.Conversion, "A", T0.AddMinutes(5), 10m),
            Ev(EventType.Click, "B", T0.AddMinutes(6)),
            Ev(EventType.Conversion, "B", T0.AddMinutes(7), 2.5m),
            Ev(EventType.Conversion, "B", T0.AddMinutes(8), 2.5m));
        var client = factory.CreateClient();

        var rows = await client.GetFromJsonAsync<JsonElement[]>(
            $"/v1/reports/campaigns?from={T0:O}&to={T0.AddDays(1):O}");

        Assert.NotNull(rows);
        Assert.Equal(2, rows!.Length);
        var a = rows[0];
        Assert.Equal("A", a.GetProperty("campaignId").GetString());
        Assert.Equal(4, a.GetProperty("clicks").GetInt32());
        Assert.Equal(1, a.GetProperty("conversions").GetInt32());
        Assert.Equal(10m, a.GetProperty("revenue").GetDecimal());
        Assert.Equal(0.25m, a.GetProperty("conversionRate").GetDecimal());
        var b = rows[1];
        Assert.Equal("B", b.GetProperty("campaignId").GetString());
        Assert.Equal(5m, b.GetProperty("revenue").GetDecimal());
        Assert.Equal(2m, b.GetProperty("conversionRate").GetDecimal());
    }

    [Fact]
    public async Task Report_only_counts_events_inside_the_window()
    {
        using var factory = new ApiFactory();
        await Seed(factory,
            Ev(EventType.Click, "A", T0.AddHours(-1)),   // before
            Ev(EventType.Click, "A", T0),                // inclusive start
            Ev(EventType.Click, "A", T0.AddHours(1)),    // exclusive end
            Ev(EventType.Click, "A", T0.AddHours(2)));   // after
        var client = factory.CreateClient();

        var rows = await client.GetFromJsonAsync<JsonElement[]>(
            $"/v1/reports/campaigns?from={T0:O}&to={T0.AddHours(1):O}");

        Assert.Single(rows!);
        Assert.Equal(1, rows![0].GetProperty("clicks").GetInt32());
    }

    [Fact]
    public async Task Report_with_no_clicks_reports_zero_rate_not_error()
    {
        using var factory = new ApiFactory();
        await Seed(factory, Ev(EventType.Conversion, "A", T0.AddMinutes(1), 3m));
        var client = factory.CreateClient();

        var rows = await client.GetFromJsonAsync<JsonElement[]>(
            $"/v1/reports/campaigns?from={T0:O}&to={T0.AddDays(1):O}");

        Assert.Equal(0m, rows![0].GetProperty("conversionRate").GetDecimal());
    }

    [Theory]
    [InlineData("")]
    [InlineData("?from=2026-01-02T00:00:00Z&to=2026-01-01T00:00:00Z")]
    [InlineData("?from=2026-01-01T00:00:00Z&to=2026-01-01T00:00:00Z")]
    public async Task Report_rejects_missing_or_inverted_window(string query)
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/reports/campaigns" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
