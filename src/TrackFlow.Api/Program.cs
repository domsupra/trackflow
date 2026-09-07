using Microsoft.EntityFrameworkCore;
using TrackFlow.Api.Data;
using TrackFlow.Api.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TrackingDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Tracking") ?? "Data Source=trackflow.db"));

var app = builder.Build();

// Sample-project shortcut: create the schema on startup. A real deployment would use migrations.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<TrackingDbContext>().Database.EnsureCreated();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapEventEndpoints();

app.Run();

// Exposes the entry point to WebApplicationFactory in the test project.
public partial class Program { }
