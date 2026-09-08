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
/// One isolated named in-memory SQLite database per test, in shared-cache mode: the same
/// pattern <c>Program.cs</c> uses in demo mode, so tests exercise the app's real
/// connection-per-context registration. A single idle connection pins the database's
/// lifetime; it is disposed (with the database) when the factory is disposed.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly DbConnection _pin;

    private readonly string _connectionString;

    public ApiFactory()
    {
        // A unique named database per instance, so parallel test classes never share state.
        // Each DbContext opens (and disposes) its own connection; the pin holds the
        // database itself alive between them.
        _connectionString = $"Data Source=trackflow-test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _pin = new SqliteConnection(_connectionString);
        _pin.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Replace the app's own registration with one that uses this test's database.
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TrackingDbContext>>();
            // String form: each context opens its own connection to the shared-cache
            // in-memory database; the pin above keeps the database alive.
            services.AddDbContext<TrackingDbContext>(o => o.UseSqlite(_connectionString));
        });
    }

    public TrackingDbContext CreateDb()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pin.Dispose(); // last connection closes; the in-memory database goes with it
        }
        base.Dispose(disposing);
    }
}
