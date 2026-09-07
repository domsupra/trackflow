using TrackFlow.Api.Data;

namespace TrackFlow.Api.Rollup;

public class RollupOptions
{
    public const string Section = "Rollup";
    public int IntervalMinutes { get; set; } = 5;
}

/// <summary>Runs <see cref="RollupService"/> on a fixed interval. Failures are logged, never fatal.</summary>
public class RollupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RollupWorker> _log;
    private readonly TimeSpan _interval;

    public RollupWorker(IServiceScopeFactory scopes, ILogger<RollupWorker> log, IConfiguration config)
    {
        _scopes = scopes;
        _log = log;
        var opts = config.GetSection(RollupOptions.Section).Get<RollupOptions>() ?? new RollupOptions();
        _interval = TimeSpan.FromMinutes(Math.Max(1, opts.IntervalMinutes));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
                await new RollupService(db).RollupAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Hourly rollup failed; will retry next tick");
            }
        }
    }
}
