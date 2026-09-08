using Microsoft.EntityFrameworkCore;
using TrackFlow.Api.Data;
using TrackFlow.Api.Events;
using TrackFlow.Api.Reports;
using TrackFlow.Api.Rollup;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TrackingDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Tracking") ?? "Data Source=trackflow.db"));
builder.Services.AddHostedService<RollupWorker>();

var app = builder.Build();

// Sample-project shortcut: create the schema on startup. A real deployment would use migrations.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<TrackingDbContext>().Database.EnsureCreated();
}

// The React dashboard (dashboard/) builds into wwwroot (see `npm run build`); when the
// bundle is present, the same process serves the UI and the API. Without it the app is
// pure JSON — the dashboard is a deployment convenience, not a dependency. WebRootPath is
// the content-root web root by default, overridable in tests.
var webRoot = app.Environment.WebRootPath;
if (Directory.Exists(webRoot) && File.Exists(Path.Combine(webRoot, "index.html")))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapEventEndpoints();
app.MapReportEndpoints();

app.Run();

// Exposes the entry point to WebApplicationFactory in the test project.
public partial class Program { }
