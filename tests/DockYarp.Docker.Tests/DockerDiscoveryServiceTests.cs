namespace DockYarp.Docker.Tests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Core.Configuration;
using DockYarp.Core.Stores;
using DockYarp.Docker.Discovery;
using DockYarp.Docker.Labels;
using DockYarp.Docker.Models;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Tests for <see cref="DockerDiscoveryService"/> (event handling and reconnect).</summary>
public sealed class DockerDiscoveryServiceTests
{
    /// <summary>A lifecycle event triggers a reconciliation that reflects the new container set.</summary>
    [Test]
    public async Task EventTriggersReconcile()
    {
        FakeContainerSource source = new();
        RouteConfigStore store = new();
        DockerDiscoveryService service = CreateService(source, store);
        source.SetContainers(Replica("c1", "10.0.0.1"));

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() => store.Current.Routes.Any(route => route.HostPattern == "app.local"));

            source.SetContainers(Replica("c1", "10.0.0.1"), Replica("c2", "10.0.0.2"));
            source.RaiseEvent(new ContainerLifecycleEvent(ContainerEventKind.Started, "c2"));

            await WaitUntilAsync(() => store.Current.Clusters.Any(cluster => cluster.Endpoints.Length == 2));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>A burst of events arriving together collapses into a single reconciliation.</summary>
    [Test]
    public async Task CoalescesBurstIntoSingleReconcile()
    {
        FakeContainerSource source = new();
        RouteConfigStore store = new();
        DockerDiscoveryService service = CreateService(source, store);
        source.SetContainers(Replica("c1", "10.0.0.1"));

        await service.StartAsync(CancellationToken.None);
        try
        {
            // Startup reconcile: wait until the initial route is published (one list call so far).
            await WaitUntilAsync(() => store.Current.Routes.Any(route => route.HostPattern == "app.local"));
            int afterStartup = source.ListCallCount;

            // Queue a burst of events together; the debounce window must fold them into one reconcile.
            for (int index = 0; index < 6; index++)
            {
                source.RaiseEvent(new ContainerLifecycleEvent(ContainerEventKind.Started, "c1"));
            }

            // Wait comfortably past the debounce cap, then assert a single extra reconcile ran for the burst.
            await Task.Delay(400);
            (source.ListCallCount - afterStartup).Should().Be(1);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>After a watch failure the service reconnects and reconciles again.</summary>
    [Test]
    public async Task ReconnectsAfterWatchFailure()
    {
        FakeContainerSource source = new();
        RouteConfigStore store = new();
        DockerDiscoveryService service = CreateService(source, store);
        source.FailNextWatch(1);
        source.SetContainers(Replica("c1", "10.0.0.1"));

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() =>
                source.ListCallCount >= 2 && store.Current.Routes.Any(route => route.HostPattern == "app.local"));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private static DockerDiscoveryService CreateService(FakeContainerSource source, RouteConfigStore store)
    {
        DiscoveryReconciler reconciler = new(source, store, new EmptyStaticConfigProvider(), NullLogger<DiscoveryReconciler>.Instance);
        DockerDiscoveryOptions options = new()
        {
            InitialReconnectDelay = TimeSpan.FromMilliseconds(5),
            MaxReconnectDelay = TimeSpan.FromMilliseconds(20),
            ReconcileDebounceMin = TimeSpan.FromMilliseconds(20),
            ReconcileDebounceMax = TimeSpan.FromMilliseconds(200),
        };
        return new DockerDiscoveryService(
            source, reconciler, options, new DiscoveryHealthState(), TimeProvider.System, NullLogger<DockerDiscoveryService>.Instance);
    }

    private static ContainerInfo Replica(string id, string address) =>
        DiscoveryTestData.Container(
            id,
            address,
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080")));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int elapsed = 0; elapsed < 2000 && !condition(); elapsed += 10)
        {
            await Task.Delay(10);
        }

        condition().Should().BeTrue();
    }
}
