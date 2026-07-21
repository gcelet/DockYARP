namespace DockYarp.Core.Configuration;

using System.Collections.Immutable;

using DockYarp.Core.Models;

/// <summary>Routes and clusters contributed by a single configuration source.</summary>
/// <param name="Source">Origin of this contribution.</param>
/// <param name="Routes">Routes contributed by the source.</param>
/// <param name="Clusters">Clusters contributed by the source.</param>
public sealed record ConfigContribution(
    ConfigSource Source,
    ImmutableArray<RouteRule> Routes,
    ImmutableArray<Cluster> Clusters);
