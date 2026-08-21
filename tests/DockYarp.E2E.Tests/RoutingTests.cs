namespace DockYarp.E2E.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using AwesomeAssertions;

/// <summary>Routing scenarios: path rewriting, multi-port mapping, and route precedence.</summary>
[Category("EndToEnd")]
public sealed class RoutingTests : E2ETestBase
{
    /// <summary><c>VIRTUAL_PATH=/api</c> with <c>VIRTUAL_DEST=/</c> strips the prefix before forwarding.</summary>
    [Test]
    public async Task PathRewrite_StripsPrefix()
    {
        using HttpResponseMessage response = await PollUntilSuccessAsync("echo.local", "/api/orders");

        JsonElement echo = await ReadJsonAsync(response);
        echo.GetProperty("path").GetString().Should().Be("/orders");
    }

    /// <summary><c>VIRTUAL_HOST_MULTIPORTS</c> routes different paths to different container ports.</summary>
    [Test]
    public async Task MultiPort_RoutesPerPath()
    {
        using HttpResponseMessage root = await PollUntilSuccessAsync("multiport.local", "/");
        using HttpResponseMessage api = await PollUntilSuccessAsync("multiport.local", "/api");

        JsonElement rootEcho = await ReadJsonAsync(root);
        JsonElement apiEcho = await ReadJsonAsync(api);
        rootEcho.GetProperty("port").GetInt32().Should().Be(8080);
        apiEcho.GetProperty("port").GetInt32().Should().Be(8081);
    }

    /// <summary><c>DOCKYARP_AFFINITY=ip-hash</c> sticks repeated requests from one client to one replica —
    /// proves the custom <c>ISessionAffinityPolicy</c> is actually wired into the live YARP pipeline (DI
    /// registration, middleware ordering, real client-IP recovery), which no unit or integration test can prove
    /// without a real network connection.</summary>
    [Test]
    public async Task Affinity_StickyClientRoutesToSameBackend()
    {
        using HttpResponseMessage first = await PollUntilSuccessAsync("affinity.local", "/");
        JsonElement firstEcho = await ReadJsonAsync(first);
        string? expected = EchoId(firstEcho);
        expected.Should().NotBeNull("the affinity backends set BACKEND_ID");

        List<string?> ids = [expected];
        for (int i = 0; i < 9; i++)
        {
            using HttpResponseMessage response = await Proxy.SendAsync(Request(HttpMethod.Get, "affinity.local", "/"));
            JsonElement echo = await ReadJsonAsync(response);
            ids.Add(EchoId(echo));
        }

        ids.Should().AllSatisfy(id => id.Should().Be(expected), "every request from this one client must stick to the same replica");
        ids.Distinct(System.StringComparer.Ordinal).Should().ContainSingle();
    }
}
