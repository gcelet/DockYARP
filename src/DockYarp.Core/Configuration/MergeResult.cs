namespace DockYarp.Core.Configuration;

using System.Collections.Immutable;

using DockYarp.Core.Models;

/// <summary>The outcome of merging configuration contributions.</summary>
/// <param name="Routes">The merged routes in deterministic order.</param>
/// <param name="Clusters">The merged clusters in deterministic order.</param>
/// <param name="Diagnostics">Conflicts and skipped entries encountered while merging.</param>
public sealed record MergeResult(
    ImmutableArray<RouteRule> Routes,
    ImmutableArray<Cluster> Clusters,
    ImmutableArray<MergeDiagnostic> Diagnostics);
