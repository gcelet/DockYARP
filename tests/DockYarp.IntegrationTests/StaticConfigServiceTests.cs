namespace DockYarp.IntegrationTests;

using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.App.StaticConfig;
using DockYarp.Core.Configuration;
using DockYarp.Core.Models;
using DockYarp.Core.Stores;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Tests for <see cref="StaticConfigService"/> (startup applier used when discovery is off).</summary>
public sealed class StaticConfigServiceTests
{
    /// <summary>The static contribution is merged and applied to the store at startup.</summary>
    [Test]
    public async Task AppliesStaticConfigAtStartup()
    {
        RouteConfigStore store = new();
        Cluster cluster = new()
        {
            Id = "api",
            Endpoints = [new ClusterEndpoint("e1", "http://10.0.0.9:8080")],
        };
        RouteRule route = new() { HostPattern = "api.local", ClusterId = "api" };
        StaticProvider provider = new(new ConfigContribution(ConfigSource.Static, [route], [cluster]));
        StaticConfigService service = new(provider, store, NullLogger<StaticConfigService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        store.Current.Routes.Should().ContainSingle(r => r.HostPattern == "api.local");
        store.Current.Clusters.Should().ContainSingle(c => c.Id == "api");
    }

    private sealed class StaticProvider(ConfigContribution contribution) : IStaticConfigProvider
    {
        public ConfigContribution GetContribution() => contribution;
    }
}
