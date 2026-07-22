namespace DockYarp.IntegrationTests;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.App.ReverseProxy;
using DockYarp.Core.Configuration;
using DockYarp.Core.Models;
using DockYarp.Core.Stores;

using Yarp.ReverseProxy.Configuration;

/// <summary>Tests for <see cref="YarpConfigBridge"/>.</summary>
public sealed class YarpConfigBridgeTests
{
    /// <summary>A store change updates YARP's in-memory configuration.</summary>
    [Test]
    public async Task StoreChangeUpdatesYarpConfig()
    {
        RouteConfigStore store = new();
        InMemoryConfigProvider provider = new([], []);
        YarpConfigBridge bridge = new(store, provider, new RoutingOptions());
        await bridge.StartAsync(CancellationToken.None);

        try
        {
            store.Apply(
                [new RouteRule { HostPattern = "app.local", ClusterId = "app" }],
                [new Cluster { Id = "app", Endpoints = [new ClusterEndpoint("c1", "http://10.0.0.1:8080")] }]);

            provider.GetConfig().Routes.Should().ContainSingle(route => route.ClusterId == "app");
            provider.GetConfig().Clusters.Should().ContainSingle(cluster => cluster.ClusterId == "app");
        }
        finally
        {
            await bridge.StopAsync(CancellationToken.None);
            bridge.Dispose();
        }
    }
}
