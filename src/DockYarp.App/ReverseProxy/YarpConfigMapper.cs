namespace DockYarp.App.ReverseProxy;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using DockYarp.Core.Models;
using DockYarp.Core.Routing;
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

    // Non-native (metadata-matched) host routes sit below native host routes; a lower order wins in YARP.
    private const int NonNativeHostOrder = 1000;
    private const string CatchAllPath = "/{**catch-all}";

    private static RouteConfig BuildRoute(RouteRule rule)
    {
        HostPattern pattern = HostPattern.Parse(rule.HostPattern);
        bool nativeHost = pattern.Kind is HostPatternKind.Exact or HostPatternKind.LeadingWildcard;
        return new RouteConfig
        {
            RouteId = $"{rule.HostPattern}|{rule.PathPrefix}",
            ClusterId = rule.ClusterId,
            Order = BuildOrder(rule.Priority, nativeHost),
            Match = new RouteMatch
            {
                // Native forms (exact, leading wildcard) use YARP host matching; non-native forms match any host
                // here and are filtered by DockYarpHostMatcherPolicy, so they must still carry a path.
                Hosts = nativeHost ? [rule.HostPattern] : null,
                Path = nativeHost ? BuildPath(rule.PathPrefix) : BuildPath(rule.PathPrefix) ?? CatchAllPath,
            },
            Metadata = BuildHostMetadata(pattern, rule.HostPattern),
            Transforms = BuildTransforms(rule.Transforms),
        };
    }

    // YARP: a lower order takes precedence, so a higher priority maps to a lower (negated) order. Non-native host
    // routes are offset below native ones while priority still orders among them.
    private static int? BuildOrder(int priority, bool nativeHost)
    {
        if (!nativeHost)
        {
            return NonNativeHostOrder - priority;
        }

        return priority == 0 ? null : -priority;
    }

    private static IReadOnlyDictionary<string, string>? BuildHostMetadata(HostPattern pattern, string hostPattern) =>
        pattern.Kind == HostPatternKind.TrailingWildcard
            ? new Dictionary<string, string>(StringComparer.Ordinal) { [DockYarpHostMatcherPolicy.HostTrailingKey] = hostPattern }
            : null;

    private static IReadOnlyList<IReadOnlyDictionary<string, string>>? BuildTransforms(RouteTransforms? transforms)
    {
        if (transforms is null)
        {
            return null;
        }

        // Built-in YARP request transforms, applied in order: strip the matched prefix (on segment boundaries),
        // then prepend the destination — so "/api" + dest "/v2" rewrites "/api/orders" to "/v2/orders".
        List<IReadOnlyDictionary<string, string>> list = [];
        if (transforms.PathRemovePrefix is { Length: > 0 } remove)
        {
            list.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["PathRemovePrefix"] = remove });
        }

        if (transforms.PathAddPrefix is { Length: > 0 } add)
        {
            list.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["PathPrefix"] = add });
        }

        return list.Count > 0 ? list : null;
    }

    private static ClusterConfig BuildCluster(Cluster cluster) =>
        new()
        {
            ClusterId = cluster.Id,
            LoadBalancingPolicy = MapPolicy(cluster.LoadBalancingPolicy),
            Destinations = BuildDestinations(cluster.Endpoints),
            HealthCheck = BuildHealth(cluster.HealthCheck),
            HttpRequest = cluster.RequestTimeout is { } timeout
                ? new ForwarderRequestConfig { ActivityTimeout = timeout }
                : null,
        };

    private static IReadOnlyDictionary<string, DestinationConfig> BuildDestinations(
        ImmutableArray<ClusterEndpoint> endpoints)
    {
        // Last-wins by endpoint id so duplicate endpoints (e.g. a repeated host or static address) never throw.
        Dictionary<string, DestinationConfig> destinations = new(StringComparer.Ordinal);
        foreach (ClusterEndpoint endpoint in endpoints)
        {
            destinations[endpoint.Id] = new DestinationConfig { Address = endpoint.Address };
        }

        return destinations;
    }

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
