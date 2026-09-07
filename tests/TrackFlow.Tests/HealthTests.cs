using System.Net;
using System.Net.Http.Json;

namespace TrackFlow.Tests;

public class HealthTests
{
    [Fact]
    public async Task Health_returns_ok_status()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("ok", body!["status"]);
    }
}
