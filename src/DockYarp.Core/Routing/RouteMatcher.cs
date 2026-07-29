namespace DockYarp.Core.Routing;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

using DockYarp.Core.Models;

/// <summary>Selects the best route for a request host and path.</summary>
/// <remarks>
/// The index is built once from a snapshot's routes. Selection prefers an exact host, then a leading wildcard
/// (<c>*.suffix</c>), then a trailing wildcard (<c>prefix.*</c>); within a tier it prefers the highest priority
/// and then the longest matching path prefix. Matching allocates nothing on the request path.
/// </remarks>
public sealed class RouteMatcher
{
    private readonly Dictionary<string, RouteRule[]> exactHosts;
    private readonly PatternRoute[] leadingWildcards;
    private readonly PatternRoute[] trailingWildcards;
    private readonly PatternRoute[] regexHosts;
    private readonly RouteRule[]? defaultHostRoutes;

    /// <summary>Builds a matcher from the given routes, with no default host.</summary>
    /// <param name="routes">The routes to index.</param>
    public RouteMatcher(ImmutableArray<RouteRule> routes)
        : this(routes, defaultHost: null)
    {
    }

    /// <summary>Builds a matcher from the given routes.</summary>
    /// <param name="routes">The routes to index.</param>
    /// <param name="defaultHost">Host whose routes also serve requests matching no other host, or <see langword="null"/>.</param>
    public RouteMatcher(ImmutableArray<RouteRule> routes, string? defaultHost)
    {
        Dictionary<string, List<RouteRule>> exact = new(StringComparer.OrdinalIgnoreCase);
        List<PatternRoute> leading = [];
        List<PatternRoute> trailing = [];
        List<PatternRoute> regexes = [];

        foreach (RouteRule route in routes)
        {
            HostPattern pattern = HostPattern.Parse(route.HostPattern);
            switch (pattern.Kind)
            {
                case HostPatternKind.LeadingWildcard:
                {
                    leading.Add(new PatternRoute(pattern, route));
                    break;
                }

                case HostPatternKind.TrailingWildcard:
                {
                    trailing.Add(new PatternRoute(pattern, route));
                    break;
                }

                case HostPatternKind.Regex:
                {
                    regexes.Add(new PatternRoute(pattern, route));
                    break;
                }

                default:
                {
                    if (exact.TryGetValue(route.HostPattern, out List<RouteRule>? bucket))
                    {
                        bucket.Add(route);
                    }
                    else
                    {
                        exact.Add(route.HostPattern, [route]);
                    }

                    break;
                }
            }
        }

        exactHosts = BuildExactIndex(exact);
        leading.Sort(static (left, right) => CompareRoutes(left.Rule, right.Rule));
        trailing.Sort(static (left, right) => CompareRoutes(left.Rule, right.Rule));
        regexes.Sort(static (left, right) => CompareRoutes(left.Rule, right.Rule));
        leadingWildcards = [.. leading];
        trailingWildcards = [.. trailing];
        regexHosts = [.. regexes];
        defaultHostRoutes = defaultHost is { Length: > 0 } host && exactHosts.TryGetValue(host, out RouteRule[]? routesForHost)
            ? routesForHost
            : null;
    }

    /// <summary>Attempts to select the route matching the given host and path.</summary>
    /// <param name="host">Request host (without port).</param>
    /// <param name="path">Request path.</param>
    /// <param name="route">The selected route when a match is found.</param>
    /// <returns><see langword="true"/> when a route matches; otherwise <see langword="false"/>.</returns>
    public bool TryMatch(string host, string path, [MaybeNullWhen(false)] out RouteRule route)
    {
        if (exactHosts.TryGetValue(host, out RouteRule[]? bucket) && TryMatchPath(bucket, path, out route))
        {
            return true;
        }

        if (TryMatchWildcard(leadingWildcards, host, path, out route)
            || TryMatchWildcard(trailingWildcards, host, path, out route)
            || TryMatchWildcard(regexHosts, host, path, out route))
        {
            return true;
        }

        // Fallback: the configured default host also serves requests matching no other host.
        if (defaultHostRoutes is not null && TryMatchPath(defaultHostRoutes, path, out route))
        {
            return true;
        }

        route = null;
        return false;
    }

    [SuppressMessage(
        "SonarAnalyzer",
        "S3267:Loops should be simplified with LINQ",
        Justification = "Request hot path: an explicit loop avoids LINQ allocations/closures per the low-allocation guideline.")]
    private static bool TryMatchWildcard(
        PatternRoute[] wildcards, string host, string path, [MaybeNullWhen(false)] out RouteRule route)
    {
        foreach (PatternRoute wildcard in wildcards)
        {
            if (wildcard.Pattern.Matches(host) && PathMatches(wildcard.Rule.PathPrefix, path))
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

        // A prefix path is more specific than a regex path, so it is tried first.
        int byRegex = IsRegexPath(left.PathPrefix).CompareTo(IsRegexPath(right.PathPrefix));
        if (byRegex != 0)
        {
            return byRegex;
        }

        return PathLength(right.PathPrefix).CompareTo(PathLength(left.PathPrefix));
    }

    private static bool PathMatches(string? prefix, string path)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return true;
        }

        return IsRegexPath(prefix)
            ? CompiledRegexCache.IsMatch(prefix[1..], path)
            : path.StartsWith(prefix, StringComparison.Ordinal);
    }

    private static bool IsRegexPath(string? prefix) => prefix is { Length: > 0 } && prefix[0] == '~';

    private static int PathLength(string? prefix) => prefix?.Length ?? 0;

    private readonly record struct PatternRoute(HostPattern Pattern, RouteRule Rule);
}
