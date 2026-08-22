namespace DockYarp.E2E.Tests;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

using Docker.DotNet.Models;

/// <summary>Host-network scenario: a backend outside DCP's network, reached via <c>Docker:HostAddress</c>.</summary>
/// <remarks>Distinct class from <see cref="NonDcpHarnessTests"/> and <see cref="MultiNetworkTests"/> — proves
/// the host-network routing contract specifically, not the harness's own generic contract.</remarks>
[Category("EndToEnd")]
public sealed class HostNetworkModeTests : E2ETestBase
{
    private const string EchoImage = "dockyarp-e2e-backend:local";
    private const string Host = "hostnet.local";
    private const string Port = "18080";
    private const string BackendId = "hostnet";

    private static NonDcpHarness harness = null!;

    [OneTimeSetUp]
    public static async Task StartBackendAsync()
    {
        harness = new NonDcpHarness();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        CancellationToken token = cts.Token;

        await harness.PullImageIfMissingAsync(EchoImage, token);
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
        await harness.RunContainerAsync(EchoImage, labels, new HostConfig { NetworkMode = "host" }, token, env);
    }

    [OneTimeTearDown]
    public static async Task DisposeHarnessAsync() => await harness.DisposeAsync();

    /// <summary>A host-network backend, configured via <c>Docker:HostAddress</c>, is reached through DockYarp.</summary>
    /// <remarks>
    /// Polls for the specific expected <c>id</c>, not merely a 2xx: an unmatched host still returns a successful
    /// 200 from the default backend, so a plain success-poll would accept a not-yet-discovered container's
    /// request before discovery ever catches up, masking a real routing failure as a false pass.
    /// </remarks>
    [Test]
    public async Task HostNetworkBackend_IsReachedThroughDockYarp()
    {
        JsonElement echo = await PollJsonAsync(
            () => Request(HttpMethod.Get, Host, "/"),
            body => EchoId(body) == BackendId);

        EchoId(echo).Should().Be(BackendId);
    }
}
