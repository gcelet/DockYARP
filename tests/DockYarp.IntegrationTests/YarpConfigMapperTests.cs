namespace DockYarp.IntegrationTests;

using System.Linq;

using AwesomeAssertions;

using DockYarp.App.ReverseProxy;
using DockYarp.Core.Models;

using Yarp.ReverseProxy.Configuration;

/// <summary>Tests for <see cref="YarpConfigMapper"/>.</summary>
public sealed class YarpConfigMapperTests
{
    /// <summary>A route maps to a YARP route matching the host and target cluster.</summary>
    [Test]
    public void RouteMapsToYarpRoute()
    {
        RouteConfigSnapshot snapshot = new(
            [new RouteRule { HostPattern = "app.local", PathPrefix = "/api", ClusterId = "app" }],
            [Cluster("app", LoadBalancingPolicy.RoundRobin)],
            1);

        (System.Collections.Generic.IReadOnlyList<RouteConfig> routes, _) = YarpConfigMapper.Map(snapshot);

        RouteConfig route = routes.Single();
        route.ClusterId.Should().Be("app");
        route.Match.Hosts.Should().Contain("app.local");
        route.Match.Path.Should().Be("/api/{**catch-all}");
    }

    /// <summary>A cluster maps to a YARP cluster with its endpoints and load-balancing policy.</summary>
    [Test]
    public void ClusterMapsWithEndpointsAndPolicy()
    {
        RouteConfigSnapshot snapshot = new(
            [new RouteRule { HostPattern = "app.local", ClusterId = "app" }],
            [Cluster("app", LoadBalancingPolicy.LeastRequests)],
            1);

        (_, System.Collections.Generic.IReadOnlyList<ClusterConfig> clusters) = YarpConfigMapper.Map(snapshot);

        ClusterConfig cluster = clusters.Single();
        cluster.LoadBalancingPolicy.Should().Be("LeastRequests");
        cluster.Destinations.Should().HaveCount(2);
    }

    /// <summary>A cluster health-check configuration maps to YARP active health checks.</summary>
    [Test]
    public void HealthCheckMapsToYarp()
    {
        Cluster withHealth = Cluster("app", LoadBalancingPolicy.RoundRobin) with
        {
            HealthCheck = new DockYarp.Core.Models.HealthCheckConfig
            {
                ActiveEnabled = true,
                Path = "/health",
                Interval = System.TimeSpan.FromSeconds(10),
            },
        };
        RouteConfigSnapshot snapshot = new(
            [new RouteRule { HostPattern = "app.local", ClusterId = "app" }],
            [withHealth],
            1);

        (_, System.Collections.Generic.IReadOnlyList<ClusterConfig> clusters) = YarpConfigMapper.Map(snapshot);

        clusters.Single().HealthCheck.Should().NotBeNull();
        clusters.Single().HealthCheck!.Active!.Path.Should().Be("/health");
    }

    private static Cluster Cluster(string id, LoadBalancingPolicy policy) =>
        new()
        {
            Id = id,
            LoadBalancingPolicy = policy,
            Endpoints =
            [
                new ClusterEndpoint("c1", "http://10.0.0.1:8080"),
                new ClusterEndpoint("c2", "http://10.0.0.2:8080"),
            ],
        };
}
