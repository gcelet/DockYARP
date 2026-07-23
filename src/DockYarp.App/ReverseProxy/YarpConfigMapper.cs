namespace DockYarp.App.ReverseProxy;

using System;
using System.Collections.Generic;
using System.Linq;

using DockYarp.Core.Models;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

using CoreHealthCheck = DockYarp.Core.Models.HealthCheckConfig;
using YarpHealthCheck = Yarp.ReverseProxy.Configuration.HealthCheckConfig;

/// <summary>Maps the internal routing snapshot to YARP configuration objects.</summary>
public static class YarpConfigMapper
{
    private const string RoundRobin = "RoundRobin";
    private const string LeastRequests = "LeastRequests";

    /// <summary>Maps a snapshot to YARP routes and clusters, with no default host.</summary>
    /// <param name="snapshot">The routing snapshot.</param>
    /// <returns>The YARP route and cluster configuration.</returns>
    public static (IReadOnlyList<RouteConfig> Routes, IReadOnlyList<ClusterConfig> Clusters) Map(
        RouteConfigSnapshot snapshot) => Map(snapshot, defaultHost: null);

    /// <summary>Maps a snapshot to YARP routes and clusters.</summary>
    /// <param name="snapshot">The routing snapshot.</param>
    /// <param name="defaultHost">Host whose backend also serves requests matching no other host, or <see langword="null"/>.</param>
    /// <returns>The YARP route and cluster configuration.</returns>
    public static (IReadOnlyList<RouteConfig> Routes, IReadOnlyList<ClusterConfig> Clusters) Map(
        RouteConfigSnapshot snapshot,
        string? defaultHost)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        List<RouteConfig> routeList = [.. snapshot.Routes.Select(BuildRoute)];
        if (BuildDefaultRoute(snapshot, defaultHost) is { } catchAll)
        {
            routeList.Add(catchAll);
        }

        IReadOnlyList<ClusterConfig> clusters = [.. snapshot.Clusters.Select(BuildCluster)];
        return (routeList, clusters);
    }

    private static RouteConfig? BuildDefaultRoute(RouteConfigSnapshot snapshot, string? defaultHost)
    {
        if (defaultHost is not { Length: > 0 })
        {
            return null;
        }

        RouteRule? target = snapshot.Routes.FirstOrDefault(
            rule => string.Equals(rule.HostPattern, defaultHost, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return null;
        }

        // No host match => any host; near-lowest precedence so specific host routes win, but it still
        // beats the terminal MapFallback (which sits at int.MaxValue) so unknown hosts reach the backend.
        return new RouteConfig
        {
            RouteId = "__default_host__",
            ClusterId = target.ClusterId,
            Order = int.MaxValue - 1,
            Match = new RouteMatch { Path = "/{**catch-all}" },
            Transforms = BuildTransforms(target.Transforms),
        };
    }

    private static RouteConfig BuildRoute(RouteRule rule) =>
        new()
        {
            RouteId = $"{rule.HostPattern}|{rule.PathPrefix}",
            ClusterId = rule.ClusterId,

            // YARP: a lower order takes precedence, so a higher priority maps to a lower (negated) order.
            Order = rule.Priority == 0 ? null : -rule.Priority,
            Match = new RouteMatch
            {
                Hosts = [rule.HostPattern],
                Path = BuildPath(rule.PathPrefix),
            },
            Transforms = BuildTransforms(rule.Transforms),
        };

    private static IReadOnlyList<IReadOnlyDictionary<string, string>>? BuildTransforms(RouteTransforms? transforms)
    {
        if (transforms?.PathRemovePrefix is not { Length: > 0 } prefix)
        {
            return null;
        }

        // Built-in YARP request transform: strips the matching prefix (on segment boundaries) before forwarding.
        return [new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["PathRemovePrefix"] = prefix }];
    }

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
            HttpRequest = cluster.RequestTimeout is { } timeout
                ? new ForwarderRequestConfig { ActivityTimeout = timeout }
                : null,
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
