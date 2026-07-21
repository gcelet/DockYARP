namespace DockYarp.Core.Tests;

using System.Collections.Immutable;

using AwesomeAssertions;

using DockYarp.Core.Models;
using DockYarp.Core.Stores;

/// <summary>Tests for <see cref="RouteConfigStore"/>.</summary>
public sealed class RouteConfigStoreTests
{
    /// <summary>Applying content publishes it and moves the version past the empty baseline.</summary>
    [Test]
    public void ApplyPublishesContentAndBumpsVersion()
    {
        RouteConfigStore store = new();
        ImmutableArray<RouteRule> routes = [new RouteRule { HostPattern = "app.local", ClusterId = "app" }];
        ImmutableArray<Cluster> clusters = [ClusterFor("app")];

        RouteConfigSnapshot published = store.Apply(routes, clusters);

        published.Routes.Should().HaveCount(1);
        published.Version.Should().Be(1);
        store.Current.Should().BeSameAs(published);
    }

    /// <summary>A content change strictly increases the version.</summary>
    [Test]
    public void VersionIncrementsOnContentChange()
    {
        RouteConfigStore store = new();
        store.Apply([new RouteRule { HostPattern = "a.local", ClusterId = "a" }], [ClusterFor("a")]);

        RouteConfigSnapshot second = store.Apply(
            [new RouteRule { HostPattern = "b.local", ClusterId = "b" }],
            [ClusterFor("b")]);

        second.Version.Should().Be(2);
    }

    /// <summary>Re-applying identical content is a no-op: same reference, same version.</summary>
    [Test]
    public void NoOpUpdateKeepsSameSnapshot()
    {
        RouteConfigStore store = new();
        ImmutableArray<RouteRule> routes = [new RouteRule { HostPattern = "app.local", ClusterId = "app" }];
        ImmutableArray<Cluster> clusters = [ClusterFor("app")];
        RouteConfigSnapshot first = store.Apply(routes, clusters);

        RouteConfigSnapshot again = store.Apply(routes, clusters);

        again.Should().BeSameAs(first);
        again.Version.Should().Be(1);
    }

    /// <summary>A snapshot obtained before an update is not mutated by that update.</summary>
    [Test]
    public void ReaderSnapshotIsStableAcrossUpdate()
    {
        RouteConfigStore store = new();
        RouteConfigSnapshot before = store.Apply(
            [new RouteRule { HostPattern = "a.local", ClusterId = "a" }],
            [ClusterFor("a")]);

        store.Apply(
            [new RouteRule { HostPattern = "b.local", ClusterId = "b" }],
            [ClusterFor("b")]);

        before.Routes.Should().ContainSingle(route => route.HostPattern == "a.local");
        before.Version.Should().Be(1);
    }

    private static Cluster ClusterFor(string id) =>
        new() { Id = id, Endpoints = [new ClusterEndpoint($"{id}-1", $"http://{id}:8080")] };
}
