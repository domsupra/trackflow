using Microsoft.EntityFrameworkCore;
using TrackFlow.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TrackingDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Tracking") ?? "Data Source=trackflow.db"));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Exposes the entry point to WebApplicationFactory in the test project.
public partial class Program { }
