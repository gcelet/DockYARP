namespace DockYarp.App.ReverseProxy;

using System;
using System.Collections.Generic;
using System.Linq;

using DockYarp.Core.Models;
using Yarp.ReverseProxy.Configuration;

using CoreHealthCheck = DockYarp.Core.Models.HealthCheckConfig;
using YarpHealthCheck = Yarp.ReverseProxy.Configuration.HealthCheckConfig;

/// <summary>Maps the internal routing snapshot to YARP configuration objects.</summary>
public static class YarpConfigMapper
{
    private const string RoundRobin = "RoundRobin";
    private const string LeastRequests = "LeastRequests";

    /// <summary>Maps a snapshot to YARP routes and clusters.</summary>
    /// <param name="snapshot">The routing snapshot.</param>
    /// <returns>The YARP route and cluster configuration.</returns>
    public static (IReadOnlyList<RouteConfig> Routes, IReadOnlyList<ClusterConfig> Clusters) Map(
        RouteConfigSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        IReadOnlyList<RouteConfig> routes = [.. snapshot.Routes.Select(BuildRoute)];
        IReadOnlyList<ClusterConfig> clusters = [.. snapshot.Clusters.Select(BuildCluster)];
        return (routes, clusters);
    }

    private static RouteConfig BuildRoute(RouteRule rule) =>
        new()
        {
            RouteId = $"{rule.HostPattern}|{rule.PathPrefix}",
            ClusterId = rule.ClusterId,
            Match = new RouteMatch
            {
                Hosts = [rule.HostPattern],
                Path = BuildPath(rule.PathPrefix),
            },
        };

    private static ClusterConfig BuildCluster(Cluster cluster) =>
        new()
        {
            ClusterId = cluster.Id,
            LoadBalancingPolicy = MapPolicy(cluster.LoadBalancingPolicy),
            Destinations = cluster.Endpoints.ToDictionary(
                endpoint => endpoint.Id,
                endpoint => new DestinationConfig { Address = endpoint.Address },
                StringComparer.Ordinal),
            HealthCheck = BuildHealth(cluster.HealthCheck),
        };

    private static string? BuildPath(string? prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return null;
        }

        string trimmed = prefix.TrimEnd('/');
        return $"{trimmed}/{{**catch-all}}";
    }

    private static string MapPolicy(LoadBalancingPolicy policy) =>
        policy switch
        {
            LoadBalancingPolicy.LeastRequests => LeastRequests,
            _ => RoundRobin,
        };

    private static YarpHealthCheck? BuildHealth(CoreHealthCheck? health)
    {
        if (health is null)
        {
            return null;
        }

        return new YarpHealthCheck
        {
            Active = new ActiveHealthCheckConfig
            {
                Enabled = health.ActiveEnabled,
                Interval = health.Interval,
                Path = health.Path,
                Policy = "ConsecutiveFailures",
            },
            Passive = new PassiveHealthCheckConfig
            {
                Enabled = true,
                Policy = "TransportFailureRate",
            },
        };
    }
}
