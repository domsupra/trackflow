using Microsoft.EntityFrameworkCore;
using TrackFlow.Api.Data;
using TrackFlow.Api.Rollup;

namespace TrackFlow.Tests;

public class RollupServiceTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Event Ev(EventType type, string campaign, DateTime at, decimal amount = 0) => new()
    {
        Type = type, CampaignId = campaign, ClickId = "c", Amount = amount,
        OccurredAt = at, IdempotencyKey = Guid.NewGuid().ToString(), ReceivedAt = at,
    };

    [Fact]
    public async Task Rollup_writes_one_row_per_campaign_per_complete_hour()
    {
        using var factory = new ApiFactory();
        using (var db = factory.CreateDb())
        {
            db.Events.AddRange(
                Ev(EventType.Click, "A", T0.AddMinutes(5)),
                Ev(EventType.Click, "A", T0.AddMinutes(50)),
                Ev(EventType.Conversion, "A", T0.AddMinutes(55), 7m),
                Ev(EventType.Click, "B", T0.AddMinutes(10)),
                Ev(EventType.Click, "A", T0.AddHours(1).AddMinutes(1)),   // next hour
                Ev(EventType.Click, "A", T0.AddHours(2).AddMinutes(1)));  // incomplete hour, must be skipped
            await db.SaveChangesAsync();
        }

        using (var db = factory.CreateDb())
        {
            await new RollupService(db).RollupAsync(upToUtc: T0.AddHours(2).AddMinutes(30), CancellationToken.None);
        }

        using (var db = factory.CreateDb())
        {
            var rows = await db.CampaignHourlyStats.OrderBy(r => r.CampaignId).ThenBy(r => r.HourUtc).ToListAsync();
            Assert.Equal(3, rows.Count);
            Assert.Equal(("A", T0, 2, 1, 7m), (rows[0].CampaignId, rows[0].HourUtc, rows[0].Clicks, rows[0].Conversions, rows[0].Revenue));
            Assert.Equal(("A", T0.AddHours(1), 1, 0, 0m), (rows[1].CampaignId, rows[1].HourUtc, rows[1].Clicks, rows[1].Conversions, rows[1].Revenue));
            Assert.Equal(("B", T0, 1, 0, 0m), (rows[2].CampaignId, rows[2].HourUtc, rows[2].Clicks, rows[2].Conversions, rows[2].Revenue));
        }
    }

    [Fact]
    public async Task Rollup_run_twice_does_not_double_count()
    {
        using var factory = new ApiFactory();
        using (var db = factory.CreateDb())
        {
            db.Events.AddRange(Ev(EventType.Click, "A", T0.AddMinutes(5)), Ev(EventType.Conversion, "A", T0.AddMinutes(6), 3m));
            await db.SaveChangesAsync();
        }

        using (var db = factory.CreateDb())
            await new RollupService(db).RollupAsync(T0.AddHours(1), CancellationToken.None);
        using (var db = factory.CreateDb())
            await new RollupService(db).RollupAsync(T0.AddHours(1), CancellationToken.None);

        using (var db = factory.CreateDb())
        {
            var row = Assert.Single(await db.CampaignHourlyStats.ToListAsync());
            Assert.Equal(1, row.Clicks);
            Assert.Equal(1, row.Conversions);
            Assert.Equal(3m, row.Revenue);
        }
    }

    [Fact]
    public async Task Rollup_picks_up_hours_that_arrived_since_the_last_run()
    {
        using var factory = new ApiFactory();
        using (var db = factory.CreateDb())
        {
            db.Events.Add(Ev(EventType.Click, "A", T0.AddMinutes(5)));
            await db.SaveChangesAsync();
        }
        using (var db = factory.CreateDb())
            await new RollupService(db).RollupAsync(T0.AddHours(1), CancellationToken.None);

        using (var db = factory.CreateDb())
        {
            db.Events.Add(Ev(EventType.Click, "A", T0.AddHours(1).AddMinutes(5)));
            await db.SaveChangesAsync();
        }
        using (var db = factory.CreateDb())
            await new RollupService(db).RollupAsync(T0.AddHours(2), CancellationToken.None);

        using (var db = factory.CreateDb())
        {
            var rows = await db.CampaignHourlyStats.OrderBy(r => r.HourUtc).ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.Equal(T0.AddHours(1), rows[1].HourUtc);
        }
    }
}
