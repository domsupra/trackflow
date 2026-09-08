using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TrackFlow.Tests;

/// <summary>
/// The dashboard (dashboard/) builds into the API's <c>wwwroot</c>. When a bundle is present
/// the app serves the UI and the API from one process and falls back to <c>index.html</c>
/// for anything that isn't an API path; without it the app is pure JSON and "/" is a 404.
/// <c>Program.cs</c> gates the middleware on <c>WebRootPath</c>, so each test points the host's
/// web root at exactly the directory it wants to assert against.
/// </summary>
public class StaticFileTests
{
    private sealed class WebRootFactory : ApiFactory
    {
        private readonly string _webRoot;
        public WebRootFactory(string webRoot) => _webRoot = webRoot;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseWebRoot(_webRoot);
        }
    }

    /// <summary>The repo's built bundle, if present (CI and local <c>npm run build</c>).</summary>
    private static string? RepositoryWebRoot()
    {
        // BaseDirectory is tests/TrackFlow.Tests/bin/Debug/net7.0 → five up is the repo root.
        var candidate = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TrackFlow.Api", "wwwroot"));
        return File.Exists(Path.Combine(candidate, "index.html")) ? candidate : null;
    }

    [Fact]
    public async Task Root_is_404_when_no_bundle_is_present()
    {
        // An empty web root deterministically exercises the "no bundle" branch:
        // no static middleware, no fallback, pure JSON app.
        var empty = Path.Combine(Path.GetTempPath(), $"trackflow-webroot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(empty);
        try
        {
            using var factory = new WebRootFactory(empty);
            var client = factory.CreateClient();

            var response = await client.GetAsync("/");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }

    [Fact]
    public async Task Root_serves_the_bundle_and_client_routes_fall_back_to_index()
    {
        var webRoot = RepositoryWebRoot();
        if (webRoot is null)
        {
            // No local bundle (source-only clone). The no-bundle 404 branch above still
            // runs, so overall coverage doesn't rely on this; just no-op the serve check.
            // CI builds the dashboard before tests, so this branch only hits unbuilded dev boxes.
            return;
        }

        using var factory = new WebRootFactory(webRoot);
        var client = factory.CreateClient();

        var root = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        var body = await root.Content.ReadAsStringAsync();
        Assert.Contains("TrackFlow", body);
        Assert.Contains("root", body); // the React mount point

        // Anything that isn't an API or static-file path returns index.html (SPA fallback).
        var fallback = await client.GetAsync("/some/client/route");
        Assert.Equal(HttpStatusCode.OK, fallback.StatusCode);
        Assert.Contains("root", await fallback.Content.ReadAsStringAsync());

        // API routes keep working alongside the SPA.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }
}
