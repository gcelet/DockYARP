namespace DockYarp.App.ReverseProxy;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http;

using DockYarp.Core.Models;
using DockYarp.Core.Routing;
using DockYarp.Security;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.SessionAffinity;

using CoreHealthCheck = DockYarp.Core.Models.HealthCheckConfig;
using YarpHealthCheck = Yarp.ReverseProxy.Configuration.HealthCheckConfig;

/// <summary>Maps the internal routing snapshot to YARP configuration objects.</summary>
public static class YarpConfigMapper
{
    /// <summary>Maps a snapshot to YARP routes and clusters, with no default host and no Data Protection.</summary>
    /// <param name="snapshot">The routing snapshot.</param>
    /// <returns>The mapped routes, clusters, and any affinity-downgrade diagnostics.</returns>
    public static YarpConfigMapResult Map(RouteConfigSnapshot snapshot) =>
        Map(snapshot, defaultHost: null, dataProtection: new DataProtectionOptions());

    /// <summary>Maps a snapshot to YARP routes and clusters.</summary>
    /// <param name="snapshot">The routing snapshot.</param>
    /// <param name="defaultHost">Host whose backend also serves requests matching no other host, or <see langword="null"/>.</param>
    /// <param name="dataProtection">Data Protection options — a configured <see cref="DataProtectionOptions.CertificatePath"/>
    /// is required for the <c>Cookie</c>/<c>CustomHeader</c> affinity policies; when absent, a cluster
    /// requesting either is served with no affinity instead, and a diagnostic is returned.</param>
    /// <returns>The mapped routes, clusters, and any affinity-downgrade diagnostics.</returns>
    public static YarpConfigMapResult Map(RouteConfigSnapshot snapshot, string? defaultHost, DataProtectionOptions dataProtection)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(dataProtection);
        List<RouteConfig> routeList = [.. snapshot.Routes.Select(BuildRoute)];
        if (BuildDefaultRoute(snapshot, defaultHost) is { } catchAll)
        {
            routeList.Add(catchAll);
        }

        bool dataProtectionConfigured = dataProtection.CertificatePath is { Length: > 0 };
        List<string> diagnostics = [];
        IReadOnlyList<ClusterConfig> clusters =
            [.. snapshot.Clusters.Select(cluster => BuildCluster(cluster, dataProtectionConfigured, diagnostics))];
        return new YarpConfigMapResult { Routes = routeList, Clusters = clusters, Diagnostics = diagnostics };
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

    // Non-native (metadata-matched) host routes sit below native host routes; a lower order wins in YARP. Regex
    // sits below trailing wildcard so wildcard beats regex, matching nginx precedence.
    private const int TrailingHostOrder = 1000;
    private const int RegexHostOrder = 2000;
    private const string CatchAllPath = "/{**catch-all}";

    private static RouteConfig BuildRoute(RouteRule rule)
    {
        HostPattern pattern = HostPattern.Parse(rule.HostPattern);
        bool nativeHost = pattern.Kind is HostPatternKind.Exact or HostPatternKind.LeadingWildcard;
        bool regexPath = IsRegexPath(rule.PathPrefix);
        return new RouteConfig
        {
            RouteId = $"{rule.HostPattern}|{rule.PathPrefix}",
            ClusterId = rule.ClusterId,
            Order = BuildOrder(rule.Priority, pattern.Kind),
            Match = new RouteMatch
            {
                // Native forms (exact, leading wildcard) use YARP host matching; non-native forms match any host
                // here and are filtered by DockYarpHostMatcherPolicy, so they must still carry a path.
                Hosts = nativeHost ? [rule.HostPattern] : null,
                Path = BuildMatchPath(rule.PathPrefix, nativeHost, regexPath),
            },
            Metadata = BuildMetadata(pattern.Kind, rule.HostPattern, regexPath, rule.PathPrefix),
            Transforms = BuildTransforms(rule.Transforms),
        };
    }

    private static bool IsRegexPath(string? pathPrefix) => pathPrefix is { Length: > 0 } && pathPrefix[0] == '~';

    // A regex path is not a valid route template, so such a route matches any path and is filtered later by the
    // path matcher policy. A prefix path uses its template, and a host-only non-native route needs a catch-all.
    private static string? BuildMatchPath(string? pathPrefix, bool nativeHost, bool regexPath)
    {
        if (regexPath)
        {
            return CatchAllPath;
        }

        string? template = BuildPath(pathPrefix);
        if (template is not null)
        {
            return template;
        }

        return nativeHost ? null : CatchAllPath;
    }

    private static IReadOnlyDictionary<string, string>? BuildMetadata(
        HostPatternKind hostKind, string hostPattern, bool regexPath, string? pathPrefix)
    {
        Dictionary<string, string>? metadata = null;
        if (hostKind is HostPatternKind.TrailingWildcard or HostPatternKind.Regex)
        {
            metadata = new Dictionary<string, string>(StringComparer.Ordinal);
            metadata[DockYarpHostMatcherPolicy.HostPatternKey] = hostPattern;
        }

        if (regexPath)
        {
            metadata ??= new Dictionary<string, string>(StringComparer.Ordinal);
            metadata[DockYarpPathMatcherPolicy.PathRegexKey] = pathPrefix![1..];
        }

        return metadata;
    }

    // YARP: a lower order takes precedence, so a higher priority maps to a lower (negated) order. Non-native host
    // routes are offset below native ones (and regex below trailing) while priority still orders within a tier.
    private static int? BuildOrder(int priority, HostPatternKind kind) =>
        kind switch
        {
            HostPatternKind.TrailingWildcard => TrailingHostOrder - priority,
            HostPatternKind.Regex => RegexHostOrder - priority,
            _ => priority == 0 ? null : -priority,
        };

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

        if (transforms.ResponseHeaders is { Count: > 0 } responseHeaders)
        {
            // Override-injected response headers: set (replace) on every response (When=Always).
            foreach (KeyValuePair<string, string> header in responseHeaders)
            {
                list.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ResponseHeader"] = header.Key,
                    ["Set"] = header.Value,
                    ["When"] = "Always",
                });
            }
        }

        return list.Count > 0 ? list : null;
    }

    private static ClusterConfig BuildCluster(Cluster cluster, bool dataProtectionConfigured, List<string> diagnostics) =>
        new()
        {
            ClusterId = cluster.Id,
            LoadBalancingPolicy = MapPolicy(cluster.LoadBalancingPolicy),
            SessionAffinity = BuildSessionAffinity(cluster, dataProtectionConfigured, diagnostics),
            Destinations = BuildDestinations(cluster.Endpoints),
            HealthCheck = BuildHealth(cluster.HealthCheck),
            HttpRequest = BuildRequestConfig(cluster),
            HttpClient = BuildHttpClientConfig(cluster),
        };

    // ClientIpHash needs no Data Protection. Cookie/CustomHeader encrypt their key via Data Protection — when
    // it isn't configured, the cluster is served with no affinity (not excluded: the route itself still works
    // fine via ordinary load-balancing, matching this project's established per-container degradation idiom
    // for unsupported/invalid config — see design.md), and a diagnostic is returned for the caller to log at
    // Error (not the usual Warning, since silently downgrading to unencrypted would defeat the security
    // property the operator opted into).
    private static SessionAffinityConfig? BuildSessionAffinity(Cluster cluster, bool dataProtectionConfigured, List<string> diagnostics)
    {
        string? policyName = cluster.SessionAffinityPolicy switch
        {
            SessionAffinityPolicy.ClientIpHash => ClientIpHashSessionAffinityPolicy.PolicyName,
            SessionAffinityPolicy.Cookie when dataProtectionConfigured => SessionAffinityConstants.Policies.Cookie,
            SessionAffinityPolicy.CustomHeader when dataProtectionConfigured => SessionAffinityConstants.Policies.CustomHeader,
            _ => null,
        };

        if (policyName is not null)
        {
            return new SessionAffinityConfig
            {
                Enabled = true,
                Policy = policyName,
                FailurePolicy = SessionAffinityConstants.FailurePolicies.Redistribute,

                // Unused by ClientIpHash (stateless, nothing stored); a real, meaningful name for Cookie/CustomHeader.
                AffinityKeyName = "dockyarp-affinity",
            };
        }

        if (cluster.SessionAffinityPolicy is SessionAffinityPolicy.Cookie or SessionAffinityPolicy.CustomHeader)
        {
            string value = cluster.SessionAffinityPolicy == SessionAffinityPolicy.Cookie ? "cookie" : "custom-header";
            diagnostics.Add(
                $"Cluster '{cluster.Id}': DOCKYARP_AFFINITY={value} requires DataProtection:CertificatePath; affinity not applied.");
        }

        return null;
    }

    // Per-cluster backend HTTP client tuning; null keeps YARP's default connection pooling unchanged.
    private static HttpClientConfig? BuildHttpClientConfig(Cluster cluster) =>
        cluster.MaxConnectionsPerServer is { } max
            ? new HttpClientConfig { MaxConnectionsPerServer = max }
            : null;

    // gRPC backends require exact HTTP/2 (no downgrade); YARP then forwards gRPC, including trailers.
    private static ForwarderRequestConfig? BuildRequestConfig(Cluster cluster)
    {
        if (cluster.RequestTimeout is null && !cluster.Http2Only)
        {
            return null;
        }

        return new ForwarderRequestConfig
        {
            ActivityTimeout = cluster.RequestTimeout,
            Version = cluster.Http2Only ? HttpVersion.Version20 : null,
            VersionPolicy = cluster.Http2Only ? HttpVersionPolicy.RequestVersionExact : null,
        };
    }

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
            LoadBalancingPolicy.LeastRequests => LoadBalancingPolicies.LeastRequests,
            LoadBalancingPolicy.PowerOfTwoChoices => LoadBalancingPolicies.PowerOfTwoChoices,
            LoadBalancingPolicy.Random => LoadBalancingPolicies.Random,
            LoadBalancingPolicy.FirstAlphabetical => LoadBalancingPolicies.FirstAlphabetical,
            _ => LoadBalancingPolicies.RoundRobin,
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
