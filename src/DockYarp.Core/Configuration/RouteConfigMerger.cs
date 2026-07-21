namespace DockYarp.Core.Configuration;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using DockYarp.Core.Models;

/// <summary>Merges routes and clusters from several configuration sources into one consistent set.</summary>
/// <remarks>
/// Higher-precedence sources win on conflicts (<see cref="ConfigSource.Static"/> over
/// <see cref="ConfigSource.Dynamic"/>). Invalid entries are skipped and reported; a single bad entry never
/// discards the rest of a source. All findings are returned as diagnostics rather than logged, keeping
/// <c>DockYarp.Core</c> free of a logging dependency.
/// </remarks>
public static class RouteConfigMerger
{
    /// <summary>Merges the given contributions.</summary>
    /// <param name="contributions">Contributions from the configuration sources.</param>
    /// <returns>The merged routes and clusters plus any diagnostics.</returns>
    public static MergeResult Merge(IReadOnlyList<ConfigContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        ImmutableArray<MergeDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<MergeDiagnostic>();
        List<ConfigContribution> ordered = contributions
            .OrderByDescending(contribution => Rank(contribution.Source))
            .ToList();

        Dictionary<string, Cluster> clusters = MergeClusters(ordered, diagnostics);
        Dictionary<string, RouteRule> routes = MergeRoutes(ordered, clusters, diagnostics);

        ImmutableArray<RouteRule> orderedRoutes = routes.Values
            .OrderBy(route => route.HostPattern, StringComparer.Ordinal)
            .ThenBy(route => route.PathPrefix ?? string.Empty, StringComparer.Ordinal)
            .ThenByDescending(route => route.Priority)
            .ToImmutableArray();
        ImmutableArray<Cluster> orderedClusters = clusters.Values
            .OrderBy(cluster => cluster.Id, StringComparer.Ordinal)
            .ToImmutableArray();

        return new MergeResult(orderedRoutes, orderedClusters, diagnostics.ToImmutable());
    }

    [SuppressMessage(
        "SonarAnalyzer",
        "S3267:Loops should be simplified with LINQ",
        Justification = "The loop both adds unique clusters and reports a diagnostic on duplicates; a LINQ projection would obscure that side effect.")]
    private static Dictionary<string, Cluster> MergeClusters(
        List<ConfigContribution> ordered,
        ImmutableArray<MergeDiagnostic>.Builder diagnostics)
    {
        Dictionary<string, Cluster> clusters = new(StringComparer.Ordinal);
        foreach (ConfigContribution contribution in ordered)
        {
            foreach (Cluster cluster in contribution.Clusters)
            {
                if (!clusters.TryAdd(cluster.Id, cluster))
                {
                    diagnostics.Add(new MergeDiagnostic(
                        MergeSeverity.Warning,
                        "cluster.conflict",
                        $"Cluster '{cluster.Id}' is defined by more than one source; the higher-precedence definition is kept."));
                }
            }
        }

        return clusters;
    }

    private static Dictionary<string, RouteRule> MergeRoutes(
        List<ConfigContribution> ordered,
        Dictionary<string, Cluster> clusters,
        ImmutableArray<MergeDiagnostic>.Builder diagnostics)
    {
        Dictionary<string, RouteRule> routes = new(StringComparer.Ordinal);
        foreach (ConfigContribution contribution in ordered)
        {
            foreach (RouteRule route in contribution.Routes)
            {
                if (!IsValid(route, clusters, contribution.Source, diagnostics))
                {
                    continue;
                }

                string key = RouteKey(route);
                if (!routes.TryAdd(key, route))
                {
                    string message = $"Route for host '{route.HostPattern}' (path '{route.PathPrefix ?? "/"}') is defined by more than one source; the higher-precedence definition is kept.";
                    diagnostics.Add(new MergeDiagnostic(MergeSeverity.Warning, "route.conflict", message));
                }
            }
        }

        return routes;
    }

    private static bool IsValid(
        RouteRule route,
        Dictionary<string, Cluster> clusters,
        ConfigSource source,
        ImmutableArray<MergeDiagnostic>.Builder diagnostics)
    {
        if (string.IsNullOrWhiteSpace(route.HostPattern) || string.IsNullOrWhiteSpace(route.ClusterId))
        {
            diagnostics.Add(new MergeDiagnostic(
                MergeSeverity.Warning,
                "route.invalid",
                $"Skipped a route from the {source} source: host and cluster id are required."));
            return false;
        }

        if (!clusters.ContainsKey(route.ClusterId))
        {
            diagnostics.Add(new MergeDiagnostic(
                MergeSeverity.Warning,
                "route.cluster-missing",
                $"Skipped route for host '{route.HostPattern}': cluster '{route.ClusterId}' is not defined."));
            return false;
        }

        return true;
    }

    private static string RouteKey(RouteRule route) =>
        string.Concat(route.HostPattern.ToUpperInvariant(), "\n", route.PathPrefix ?? string.Empty);

    private static int Rank(ConfigSource source) => source == ConfigSource.Static ? 1 : 0;
}
