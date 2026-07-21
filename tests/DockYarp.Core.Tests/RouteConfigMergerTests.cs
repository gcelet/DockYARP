namespace DockYarp.Core.Tests;

using System.Collections.Immutable;

using AwesomeAssertions;

using DockYarp.Core.Configuration;
using DockYarp.Core.Models;

/// <summary>Tests for <see cref="RouteConfigMerger"/>.</summary>
public sealed class RouteConfigMergerTests
{
    /// <summary>A dynamic source alone contributes its route to the merged result.</summary>
    [Test]
    public void DynamicSourceAddsRoute()
    {
        ConfigContribution dynamic = new(
            ConfigSource.Dynamic,
            [new RouteRule { HostPattern = "app.local", ClusterId = "app" }],
            [ClusterFor("app")]);

        MergeResult result = RouteConfigMerger.Merge([dynamic]);

        result.Routes.Should().ContainSingle(route => route.HostPattern == "app.local");
    }

    /// <summary>On a host conflict the static source wins and a diagnostic is reported.</summary>
    [Test]
    public void ConflictingHostResolvedByPrecedence()
    {
        ConfigContribution dynamic = new(
            ConfigSource.Dynamic,
            [new RouteRule { HostPattern = "app.local", ClusterId = "app-dyn" }],
            [ClusterFor("app-dyn")]);
        ConfigContribution staticSource = new(
            ConfigSource.Static,
            [new RouteRule { HostPattern = "app.local", ClusterId = "app-static" }],
            [ClusterFor("app-static")]);

        MergeResult result = RouteConfigMerger.Merge([dynamic, staticSource]);

        result.Routes.Should().ContainSingle(route => route.HostPattern == "app.local")
            .Which.ClusterId.Should().Be("app-static");
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "route.conflict");
    }

    /// <summary>An invalid entry is skipped and reported while valid ones are kept.</summary>
    [Test]
    public void InvalidEntrySkippedOthersKept()
    {
        ConfigContribution dynamic = new(
            ConfigSource.Dynamic,
            [
                new RouteRule { HostPattern = "app.local", ClusterId = "app" },
                new RouteRule { HostPattern = "  ", ClusterId = "app" },
            ],
            [ClusterFor("app")]);

        MergeResult result = RouteConfigMerger.Merge([dynamic]);

        result.Routes.Should().ContainSingle(route => route.HostPattern == "app.local");
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "route.invalid");
    }

    /// <summary>A route referencing an undefined cluster is skipped and reported.</summary>
    [Test]
    public void RouteWithMissingClusterSkipped()
    {
        ConfigContribution dynamic = new(
            ConfigSource.Dynamic,
            [new RouteRule { HostPattern = "app.local", ClusterId = "ghost" }],
            []);

        MergeResult result = RouteConfigMerger.Merge([dynamic]);

        result.Routes.Should().BeEmpty();
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "route.cluster-missing");
    }

    private static Cluster ClusterFor(string id) =>
        new() { Id = id, Endpoints = [new ClusterEndpoint($"{id}-1", $"http://{id}:8080")] };
}
