namespace DockYarp.Core.Routing;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

using DockYarp.Core.Models;

/// <summary>Selects the best route for a request host and path.</summary>
/// <remarks>
/// The index is built once from a snapshot's routes. Selection prefers an exact host match over a
/// single-level wildcard (<c>*.suffix</c>), then the highest priority, then the longest matching path
/// prefix. Matching allocates nothing on the request path.
/// </remarks>
public sealed class RouteMatcher
{
    private readonly Dictionary<string, RouteRule[]> exactHosts;
    private readonly WildcardRoute[] wildcardHosts;

    /// <summary>Builds a matcher from the given routes.</summary>
    /// <param name="routes">The routes to index.</param>
    public RouteMatcher(ImmutableArray<RouteRule> routes)
    {
        Dictionary<string, List<RouteRule>> exact = new(StringComparer.OrdinalIgnoreCase);
        List<WildcardRoute> wildcards = [];

        foreach (RouteRule route in routes)
        {
            if (IsWildcard(route.HostPattern))
            {
                // "*.local" -> suffix ".local"
                wildcards.Add(new WildcardRoute(route.HostPattern[1..], route));
            }
            else if (exact.TryGetValue(route.HostPattern, out List<RouteRule>? bucket))
            {
                bucket.Add(route);
            }
            else
            {
                exact.Add(route.HostPattern, [route]);
            }
        }

        exactHosts = BuildExactIndex(exact);
        wildcards.Sort(static (left, right) => CompareRoutes(left.Rule, right.Rule));
        wildcardHosts = [.. wildcards];
    }

    /// <summary>Attempts to select the route matching the given host and path.</summary>
    /// <param name="host">Request host (without port).</param>
    /// <param name="path">Request path.</param>
    /// <param name="route">The selected route when a match is found.</param>
    /// <returns><see langword="true"/> when a route matches; otherwise <see langword="false"/>.</returns>
    [SuppressMessage(
        "SonarAnalyzer",
        "S3267:Loops should be simplified with LINQ",
        Justification = "Request hot path: an explicit loop avoids LINQ allocations/closures per the low-allocation guideline.")]
    public bool TryMatch(string host, string path, [MaybeNullWhen(false)] out RouteRule route)
    {
        if (exactHosts.TryGetValue(host, out RouteRule[]? bucket) && TryMatchPath(bucket, path, out route))
        {
            return true;
        }

        foreach (WildcardRoute wildcard in wildcardHosts)
        {
            if (MatchesWildcard(host, wildcard.Suffix) && PathMatches(wildcard.Rule.PathPrefix, path))
            {
                route = wildcard.Rule;
                return true;
            }
        }

        route = null;
        return false;
    }

    [SuppressMessage(
        "SonarAnalyzer",
        "S3267:Loops should be simplified with LINQ",
        Justification = "Request hot path: an explicit loop avoids LINQ allocations/closures per the low-allocation guideline.")]
    private static bool TryMatchPath(RouteRule[] bucket, string path, [MaybeNullWhen(false)] out RouteRule route)
    {
        // Bucket is sorted by priority (desc) then path length (desc), so the first path match is the best.
        foreach (RouteRule candidate in bucket)
        {
            if (PathMatches(candidate.PathPrefix, path))
            {
                route = candidate;
                return true;
            }
        }

        route = null;
        return false;
    }

    private static Dictionary<string, RouteRule[]> BuildExactIndex(Dictionary<string, List<RouteRule>> exact)
    {
        Dictionary<string, RouteRule[]> result = new(exact.Count, StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, List<RouteRule>> entry in exact)
        {
            entry.Value.Sort(static (left, right) => CompareRoutes(left, right));
            result.Add(entry.Key, [.. entry.Value]);
        }

        return result;
    }

    private static int CompareRoutes(RouteRule left, RouteRule right)
    {
        int byPriority = right.Priority.CompareTo(left.Priority);
        if (byPriority != 0)
        {
            return byPriority;
        }

        return PathLength(right.PathPrefix).CompareTo(PathLength(left.PathPrefix));
    }

    private static bool IsWildcard(string hostPattern) =>
        hostPattern.StartsWith("*.", StringComparison.Ordinal);

    private static bool MatchesWildcard(string host, string suffix) =>
        host.Length > suffix.Length && host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

    private static bool PathMatches(string? prefix, string path) =>
        string.IsNullOrEmpty(prefix) || path.StartsWith(prefix, StringComparison.Ordinal);

    private static int PathLength(string? prefix) => prefix?.Length ?? 0;

    private readonly record struct WildcardRoute(string Suffix, RouteRule Rule);
}
