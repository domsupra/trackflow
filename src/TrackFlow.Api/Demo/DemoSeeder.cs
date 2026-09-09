using Microsoft.EntityFrameworkCore;
using TrackFlow.Api.Data;
using TrackFlow.Api.Events;

namespace TrackFlow.Api.Demo;

public class DemoSeedOptions
{
    public const string Section = "DemoSeed";

    /// <summary>Off by default: seeding is only for a public demo instance.</summary>
    public bool Enabled { get; set; }

    /// <summary>Re-seed on this cadence so a long-running demo never shows an empty window.</summary>
    public int RefreshMinutes { get; set; } = 180;
}

/// <summary>
/// Populates the in-memory store with representative events so a public demo never
/// renders an empty dashboard.
///
/// This exists because the store is deliberately in-memory (see README): every restart
/// begins with no data, and the dashboard reports a rolling 24-hour window, so a freshly
/// started instance would show "no events in this window" to whoever opened the link.
/// Timestamps are generated relative to now for that reason — fixed historical dates
/// would fall outside the window and defeat the purpose.
///
/// Seeding is idempotent by construction: every event carries a stable idempotency key,
/// so a re-seed replays into the same rows rather than inflating the totals.
/// </summary>
public class DemoSeeder : BackgroundService
{
    private static readonly (string Campaign, int Clicks, int Conversions, decimal Low, decimal High)[] Campaigns =
    {
        ("spring-sale", 34, 9, 29.99m, 149.00m),
        ("retargeting-q2", 21, 4, 19.99m, 89.00m),
        ("brand-search", 17, 6, 49.00m, 219.00m),
        ("newsletter-drop", 12, 2, 24.50m, 62.00m),
    };

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DemoSeeder> _log;
    private readonly DemoSeedOptions _options;

    public DemoSeeder(IServiceScopeFactory scopes, ILogger<DemoSeeder> log, IConfiguration config)
    {
        _scopes = scopes;
        _log = log;
        _options = config.GetSection(DemoSeedOptions.Section).Get<DemoSeedOptions>() ?? new DemoSeedOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(5, _options.RefreshMinutes));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var written = await SeedAsync(stoppingToken);
                _log.LogInformation("Demo seed complete: {Count} events present.", written);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Demo seed failed; the dashboard may show an empty window.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Writes the demo dataset, skipping events already present by idempotency key.</summary>
    public async Task<int> SeedAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();

        var now = DateTime.UtcNow;
        // Deterministic: the same seed run produces the same amounts, so a re-seed is a
        // no-op rather than a second set of rows with different values.
        var random = new Random(11);
        var written = 0;

        foreach (var (campaign, clicks, conversions, low, high) in Campaigns)
        {
            for (var i = 0; i < clicks; i++)
            {
                var key = $"seed-{campaign}-click-{i}";
                if (await AddIfMissingAsync(db, key, new Event
                {
                    Type = EventType.Click,
                    CampaignId = campaign,
                    ClickId = $"{campaign}-c{i}",
                    Amount = 0m,
                    OccurredAt = now.AddMinutes(-random.Next(30, 1320)),
                    IdempotencyKey = key,
                    ReceivedAt = now,
                }, ct))
                {
                    written++;
                }
            }

            for (var i = 0; i < conversions; i++)
            {
                var key = $"seed-{campaign}-conv-{i}";
                var amount = Math.Round(low + (decimal)random.NextDouble() * (high - low), 2);
                if (await AddIfMissingAsync(db, key, new Event
                {
                    Type = EventType.Conversion,
                    CampaignId = campaign,
                    ClickId = $"{campaign}-c{i}",
                    Amount = amount,
                    OccurredAt = now.AddMinutes(-random.Next(20, 1200)),
                    IdempotencyKey = key,
                    ReceivedAt = now,
                }, ct))
                {
                    written++;
                }
            }
        }

        if (written > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return written;
    }

    private static async Task<bool> AddIfMissingAsync(TrackingDbContext db, string key, Event entity, CancellationToken ct)
    {
        var exists = await db.Events.AnyAsync(e => e.IdempotencyKey == key, ct);
        if (exists)
        {
            return false;
        }

        db.Events.Add(entity);
        return true;
    }
}
