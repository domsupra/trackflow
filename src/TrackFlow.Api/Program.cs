using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TrackFlow.Api.Data;
using TrackFlow.Api.Events;
using TrackFlow.Api.Reports;
using TrackFlow.Api.Rollup;

var builder = WebApplication.CreateBuilder(args);

// Malformed requests (unparseable JSON, bad query values) and unexpected failures should
// fail the same way as validation errors: RFC 9457 ProblemDetails, in every environment.
builder.Services.AddProblemDetails();

string tracking = builder.Configuration.GetConnectionString("Tracking") ?? "Data Source=:memory:";

if (tracking.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
{
    // Demo default: the store is in RAM for the lifetime of this process — no files to keep
    // up with, and every `dotnet run` starts fresh. Shared-cache mode lets every DbContext
    // open its OWN connection (required: a SqliteConnection is not safe for concurrent use,
    // and DbContext is scoped per request), while one idle connection pins the database —
    // an in-memory database dies with its last connection. DI disposes the pin at shutdown.
    const string shared = "Data Source=trackflow;Mode=Memory;Cache=Shared";
    var keepAlive = new SqliteConnection(shared);
    keepAlive.Open();
    builder.Services.AddSingleton(keepAlive);
    builder.Services.AddDbContext<TrackingDbContext>(o => o.UseSqlite(shared));
}
else
{
    builder.Services.AddDbContext<TrackingDbContext>(o => o.UseSqlite(tracking));
}
builder.Services.AddHostedService<RollupWorker>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

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

    // Catch-all for client-side routes: anything that isn't a static asset or an API route
    // must not load the SPA shell. A mistyped API path (e.g. /v1/events2) is an API mistake
    // and should 404 as JSON; only non-API paths (e.g. /campaigns/spring-sale) get index.html.
    app.MapFallback(async (HttpContext context) =>
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/v1", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync("{\"title\":\"Not found.\",\"status\":404}");
            return;
        }

        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(Path.Combine(webRoot, "index.html"));
    });
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapEventEndpoints();
app.MapReportEndpoints();

app.Run();

// Exposes the entry point to WebApplicationFactory in the test project.
public partial class Program { }
