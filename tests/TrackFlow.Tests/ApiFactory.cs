using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrackFlow.Api.Data;

namespace TrackFlow.Tests;

/// <summary>
/// One isolated SQLite in-memory database per test. The connection stays open for the
/// life of the factory so the in-memory database survives across DbContext instances.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly DbConnection _connection = new SqliteConnection("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TrackingDbContext>>();
            services.AddDbContext<TrackingDbContext>(o => o.UseSqlite(_connection));
        });
    }

    public TrackingDbContext CreateDb()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}
