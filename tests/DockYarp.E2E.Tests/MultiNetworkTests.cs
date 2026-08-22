namespace DockYarp.E2E.Tests;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

using Docker.DotNet.Models;

/// <summary>Multi-network scenario: a backend on a network DockYarp doesn't share is excluded from routing.</summary>
/// <remarks>Distinct class from <see cref="NonDcpHarnessTests"/> and <see cref="HostNetworkModeTests"/> — proves
/// the unreachable-network exclusion contract specifically, not the harness's own generic contract.</remarks>
[Category("EndToEnd")]
public sealed class MultiNetworkTests : E2ETestBase
{
    private const string EchoImage = "dockyarp-e2e-backend:local";
    private const string Host = "unreachable.local";
    private const string Port = "8080";
    private const string BackendId = "unreachable";

    private static NonDcpHarness harness = null!;

    [OneTimeSetUp]
    public static async Task StartBackendAsync()
    {
        harness = new NonDcpHarness();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        CancellationToken token = cts.Token;

        await harness.PullImageIfMissingAsync(EchoImage, token);

        // The container's only network — never connected to the Aspire session network — so it stays genuinely
        // unreachable from the proxy (unlike NonDcpHarnessTests's discovery smoke test, which connects on purpose).
        string networkId = await harness.CreateNetworkAsync("nondcp-unreachable-net", token);

        Dictionary<string, string> labels = new(StringComparer.Ordinal)
        {
            ["VIRTUAL_HOST"] = Host,
            ["VIRTUAL_PORT"] = Port,
        };
        Dictionary<string, string> env = new(StringComparer.Ordinal)
        {
            ["ASPNETCORE_URLS"] = $"http://+:{Port}",
            ["BACKEND_ID"] = BackendId,
        };
        await harness.RunContainerAsync(EchoImage, labels, new HostConfig { NetworkMode = networkId }, token, env);
    }

    [OneTimeTearDown]
    public static async Task DisposeHarnessAsync() => await harness.DisposeAsync();

    /// <summary>A backend on an unreachable network is excluded; its host falls through to the default backend.</summary>
    [Test]
    public async Task UnreachableNetworkBackend_FallsThroughToDefault()
    {
        using HttpResponseMessage response = await PollUntilSuccessAsync(Host, "/");

        JsonElement echo = await ReadJsonAsync(response);
        EchoId(echo).Should().Be("default");
    }
}
