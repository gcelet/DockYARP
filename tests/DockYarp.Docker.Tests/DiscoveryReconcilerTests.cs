namespace DockYarp.Docker.Tests;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Core.Stores;
using DockYarp.Docker.Discovery;
using DockYarp.Docker.Labels;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Tests for <see cref="DiscoveryReconciler"/>.</summary>
public sealed class DiscoveryReconcilerTests
{
    /// <summary>Reconciliation publishes the running containers into the store.</summary>
    [Test]
    public async Task ReconcilePublishesRunningContainers()
    {
        FakeContainerSource source = new();
        RouteConfigStore store = new();
        DiscoveryReconciler reconciler = new(source, store, NullLogger<DiscoveryReconciler>.Instance);
        source.SetContainers(DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080"))));

        await reconciler.ReconcileAsync(CancellationToken.None);

        store.Current.Routes.Should().ContainSingle(route => route.HostPattern == "app.local");
        store.Current.Clusters.Single().Endpoints.Should().ContainSingle();
    }

    /// <summary>When a container is gone at the next reconciliation, its route is removed.</summary>
    [Test]
    public async Task StoppedContainerIsRemovedOnReconcile()
    {
        FakeContainerSource source = new();
        RouteConfigStore store = new();
        DiscoveryReconciler reconciler = new(source, store, NullLogger<DiscoveryReconciler>.Instance);
        source.SetContainers(DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080"))));
        await reconciler.ReconcileAsync(CancellationToken.None);

        source.SetContainers();
        await reconciler.ReconcileAsync(CancellationToken.None);

        store.Current.Routes.Should().BeEmpty();
    }
}
