namespace DockYarp.E2E.Tests;

using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using AwesomeAssertions;

/// <summary>Discovery scenarios: containers are found from their labels and proxied over real networking.</summary>
[Category("EndToEnd")]
public sealed class DiscoveryTests : E2ETestBase
{
    /// <summary>A labeled whoami container is discovered and reachable through its virtual host.</summary>
    [Test]
    public async Task Whoami_IsDiscoveredAndProxied()
    {
        using HttpResponseMessage response = await PollUntilSuccessAsync("whoami.local", "/");

        response.IsSuccessStatusCode.Should().BeTrue();
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Hostname:");

        // Server-header suppression (Kestrel AddServerHeader=false) is only observable against a real
        // Kestrel front-end, so it is asserted here rather than in-process.
        response.Headers.Contains("Server").Should().BeFalse();
    }

    /// <summary>A comma-separated <c>VIRTUAL_HOST</c> exposes one container under both hosts.</summary>
    [Test]
    public async Task MultiHost_BothHostsRoute()
    {
        using HttpResponseMessage first = await PollUntilSuccessAsync("a.local", "/");
        using HttpResponseMessage second = await PollUntilSuccessAsync("b.local", "/");

        first.IsSuccessStatusCode.Should().BeTrue();
        second.IsSuccessStatusCode.Should().BeTrue();
    }

    /// <summary>The proxy adds forwarding headers the backend can observe.</summary>
    [Test]
    public async Task ForwardedHeaders_ArePropagated()
    {
        using HttpResponseMessage response = await PollUntilSuccessAsync("echo.local", "/api/");

        JsonElement echo = await ReadJsonAsync(response);
        HasHeader(echo, "X-Forwarded-For").Should().BeTrue();
        HasHeader(echo, "X-Forwarded-Proto").Should().BeTrue();
    }

    /// <summary>A request for a host with no route falls through to the configured default backend.</summary>
    [Test]
    public async Task UnknownHost_RoutesToDefaultBackend()
    {
        using HttpResponseMessage response = await PollUntilSuccessAsync("nowhere.local", "/");

        JsonElement echo = await ReadJsonAsync(response);
        EchoId(echo).Should().Be("default");
    }

    /// <summary>A backend failing its Docker health check is excluded; its host falls through to the default.</summary>
    [Test]
    public async Task UnhealthyBackend_IsExcluded()
    {
        using HttpResponseMessage response = await PollUntilSuccessAsync("unhealthy.local", "/");

        JsonElement echo = await ReadJsonAsync(response);
        EchoId(echo).Should().Be("default");
    }
}
