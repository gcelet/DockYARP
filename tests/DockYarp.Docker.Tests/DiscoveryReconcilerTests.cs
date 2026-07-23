namespace DockYarp.Docker.Tests;

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Core.Configuration;
using DockYarp.Core.Models;
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
        DiscoveryReconciler reconciler = new(source, store, new EmptyStaticConfigProvider(), NullLogger<DiscoveryReconciler>.Instance);
        source.SetContainers(DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080"))));

        await reconciler.ReconcileAsync(CancellationToken.None);

        store.Current.Routes.Should().ContainSingle(route => route.HostPattern == "app.local");
        store.Current.Clusters.Single().Endpoints.Should().ContainSingle();
    }

    /// <summary>Static configuration wins over discovery on a conflicting host.</summary>
    [Test]
    public async Task StaticConfigurationWinsOverDiscovery()
    {
        FakeContainerSource source = new();
        RouteConfigStore store = new();
        Cluster staticCluster = new()
        {
            Id = "app.local",
            Endpoints = [new ClusterEndpoint("static", "http://static:9999")],
        };
        RouteRule staticRoute = new() { HostPattern = "app.local", ClusterId = "app.local" };
        StaticProvider staticConfig = new(new ConfigContribution(ConfigSource.Static, [staticRoute], [staticCluster]));
        DiscoveryReconciler reconciler = new(source, store, staticConfig, NullLogger<DiscoveryReconciler>.Instance);
        source.SetContainers(DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080"))));

        await reconciler.ReconcileAsync(CancellationToken.None);

        store.Current.Clusters.Single(cluster => cluster.Id == "app.local")
            .Endpoints.Single().Address.Should().Be("http://static:9999");
    }

    /// <summary>When a container is gone at the next reconciliation, its route is removed.</summary>
    [Test]
    public async Task StoppedContainerIsRemovedOnReconcile()
    {
        FakeContainerSource source = new();
        RouteConfigStore store = new();
        DiscoveryReconciler reconciler = new(source, store, new EmptyStaticConfigProvider(), NullLogger<DiscoveryReconciler>.Instance);
        source.SetContainers(DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080"))));
        await reconciler.ReconcileAsync(CancellationToken.None);

        source.SetContainers();
        await reconciler.ReconcileAsync(CancellationToken.None);

        store.Current.Routes.Should().BeEmpty();
    }

    private sealed class StaticProvider(ConfigContribution contribution) : IStaticConfigProvider
    {
        public ConfigContribution GetContribution() => contribution;
    }
}
