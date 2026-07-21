namespace DockYarp.Core.Interfaces;

using System.Collections.Immutable;

using DockYarp.Core.Models;

/// <summary>Holds the active routing configuration and publishes it as atomic, versioned snapshots.</summary>
public interface IRouteConfigStore
{
    /// <summary>Gets the current snapshot. Readers never observe a partially applied update.</summary>
    RouteConfigSnapshot Current { get; }

    /// <summary>Publishes a new set of routes and clusters, replacing the current snapshot atomically.</summary>
    /// <param name="routes">The routes to publish.</param>
    /// <param name="clusters">The clusters to publish.</param>
    /// <returns>
    /// The resulting snapshot: a new version when the content changed, or the unchanged current snapshot
    /// when the supplied content is identical to it.
    /// </returns>
    RouteConfigSnapshot Apply(ImmutableArray<RouteRule> routes, ImmutableArray<Cluster> clusters);
}
