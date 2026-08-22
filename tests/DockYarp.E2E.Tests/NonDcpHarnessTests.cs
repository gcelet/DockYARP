namespace DockYarp.E2E.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

using Docker.DotNet;

/// <summary>Smoke-validates <see cref="NonDcpHarness"/> itself: a distinct suite from the future
/// <c>HostNetworkModeTests</c>/<c>MultiNetworkTests</c> (see design.md's scope boundary) that proves the
/// harness's own contract, not the scenarios those two items will build on top of it.</summary>
[Category("EndToEnd")]
public sealed class NonDcpHarnessTests : E2ETestBase
{
    private const string WhoamiImage = "traefik/whoami";
    private const string Host = "nondcp-harness.local";

    private static NonDcpHarness harness = null!;

    [OneTimeSetUp]
    public static void CreateHarness() => harness = new NonDcpHarness();

    [OneTimeTearDown]
    public static async Task DisposeHarnessAsync() => await harness.DisposeAsync();

    /// <summary>A container started directly against the Docker daemon (bypassing DCP) is still discovered.</summary>
    [Test]
    public async Task ContainerCreatedOutsideDcpIsDiscovered()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        CancellationToken token = cts.Token;

        await harness.PullImageIfMissingAsync(WhoamiImage, token);
        Dictionary<string, string> labels = new(StringComparer.Ordinal)
        {
            ["VIRTUAL_HOST"] = Host,
            ["VIRTUAL_PORT"] = "80",
        };
        string containerId = await harness.RunContainerAsync(WhoamiImage, labels, hostConfig: null, token, env: null);

        // Discovery only routes a container that shares a network with the proxy's own reachable set (see
        // ConnectToAspireSessionNetworkAsync's remarks) — Docker's default bridge alone isn't enough.
        await harness.ConnectToAspireSessionNetworkAsync(containerId, token);

        JsonElement routes = await PollJsonAsync(AdminGetRoutes, body => ContainsHost(body, Host));

        ContainsHost(routes, Host).Should().BeTrue();
    }

    /// <summary>A network created outside DCP exists once created and is gone once removed.</summary>
    [Test]
    public async Task NetworkIsCreatedAndRemoved()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        CancellationToken token = cts.Token;

        string networkId = await harness.CreateNetworkAsync("nondcp-harness-net", token);
        await harness.Client.Networks.InspectNetworkAsync(networkId, token);

        await harness.Client.Networks.DeleteNetworkAsync(networkId, token);

        Func<Task> act = async () => await harness.Client.Networks.InspectNetworkAsync(networkId, token);
        await act.Should().ThrowAsync<DockerApiException>();
    }

    private static HttpRequestMessage AdminGetRoutes()
    {
        HttpRequestMessage request = new(HttpMethod.Get, "/api/routes");
        request.Headers.Add("X-Api-Key", AspireAppHostFixture.ApiKey);
        return request;
    }

    private static bool ContainsHost(JsonElement routes, string host) =>
        routes.ValueKind == JsonValueKind.Array
        && routes.EnumerateArray().Any(route =>
            route.TryGetProperty("host", out JsonElement value) && value.GetString() == host);
}
