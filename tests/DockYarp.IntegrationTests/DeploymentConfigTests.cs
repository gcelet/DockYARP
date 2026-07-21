namespace DockYarp.IntegrationTests;

using System;

using AwesomeAssertions;

using DockYarp.Docker.Discovery;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>Tests for deployment-related host configuration.</summary>
public sealed class DeploymentConfigTests
{
    /// <summary>A bounded shutdown timeout is configured for graceful drain.</summary>
    [Test]
    public void ShutdownTimeoutIsConfigured()
    {
        using WebApplicationFactory<Program> factory = new();

        HostOptions options = factory.Services.GetRequiredService<IOptions<HostOptions>>().Value;

        options.ShutdownTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    /// <summary>Docker discovery is not wired unless explicitly enabled.</summary>
    [Test]
    public void DiscoveryIsDisabledByDefault()
    {
        using WebApplicationFactory<Program> factory = new();

        factory.Services.GetService<IContainerSource>().Should().BeNull();
    }
}
