namespace DockYarp.E2E.Tests;

using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using AwesomeAssertions;

/// <summary>Configuration-source scenarios: a container configured via environment variables (env wins over labels).</summary>
[Category("EndToEnd")]
public sealed class EnvVarConfigTests : E2ETestBase
{
    /// <summary>A backend whose VIRTUAL_HOST/PORT are set as environment variables only is discovered and routed.</summary>
    [Test]
    public async Task EnvOnlyBackend_IsDiscoveredAndProxied()
    {
        using HttpResponseMessage response = await PollUntilSuccessAsync("env.local", "/");

        JsonElement echo = await ReadJsonAsync(response);
        EchoId(echo).Should().Be("env");
    }

    /// <summary>When VIRTUAL_HOST is set as both env and label, the env value wins and the label host has no route.</summary>
    [Test]
    public async Task EnvVar_OverridesSameNamedLabel()
    {
        // The env value routes to the container...
        using HttpResponseMessage envWins = await PollUntilSuccessAsync("envwins.local", "/");
        JsonElement served = await ReadJsonAsync(envWins);
        EchoId(served).Should().Be("envwin");

        // ...while the label's host registers no route and falls through to the default backend.
        using HttpResponseMessage labelLoses = await PollUntilSuccessAsync("envlabel.local", "/");
        JsonElement fallback = await ReadJsonAsync(labelLoses);
        EchoId(fallback).Should().Be("default");
    }
}
