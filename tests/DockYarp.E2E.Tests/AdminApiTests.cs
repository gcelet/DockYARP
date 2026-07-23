namespace DockYarp.E2E.Tests;

using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using AwesomeAssertions;

/// <summary>Admin API scenarios: the sanitized read models reflect the discovered runtime state.</summary>
[Category("EndToEnd")]
public sealed class AdminApiTests : E2ETestBase
{
    /// <summary><c>GET /api/routes</c> (authenticated) lists the routes discovered from container labels.</summary>
    [Test]
    public async Task Routes_ReflectDiscoveredContainers()
    {
        JsonElement routes = await PollJsonAsync(
            () => AdminGet("/api/routes"),
            static body => ContainsHost(body, "whoami.local"));

        ContainsHost(routes, "whoami.local").Should().BeTrue();
    }

    /// <summary><c>GET /api/health</c> (authenticated) reports Docker discovery as connected.</summary>
    [Test]
    public async Task Health_ReportsDiscoveryConnected()
    {
        JsonElement health = await PollJsonAsync(
            () => AdminGet("/api/health"),
            static body => body.GetProperty("discovery").GetString() == "connected");

        health.GetProperty("discovery").GetString().Should().Be("connected");
        health.GetProperty("routes").GetInt32().Should().BeGreaterThan(0);
    }

    /// <summary>The admin API rejects a request without a valid API key.</summary>
    [Test]
    public async Task Routes_RejectMissingApiKey()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/routes");
        using HttpResponseMessage response = await Proxy.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static HttpRequestMessage AdminGet(string path)
    {
        HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("X-Api-Key", AspireAppHostFixture.ApiKey);
        return request;
    }

    private static bool ContainsHost(JsonElement routes, string host) =>
        routes.ValueKind == JsonValueKind.Array
        && routes.EnumerateArray().Any(route =>
            route.TryGetProperty("host", out JsonElement value) && value.GetString() == host);
}
