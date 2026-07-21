namespace DockYarp.Core.Models;

using System.Collections.Immutable;

/// <summary>An immutable point-in-time view of the active routes and clusters.</summary>
/// <remarks>
/// Instances are published atomically by the configuration store. Content equality intentionally ignores
/// <see cref="Version"/> and is computed with <see cref="HasSameContent"/> because <see cref="ImmutableArray{T}"/>
/// compares by underlying-array reference, not element-wise.
/// </remarks>
/// <param name="Routes">The active routing rules.</param>
/// <param name="Clusters">The active clusters.</param>
/// <param name="Version">Monotonically increasing version; changes only when the content changes.</param>
public sealed record RouteConfigSnapshot(
    ImmutableArray<RouteRule> Routes,
    ImmutableArray<Cluster> Clusters,
    long Version)
{
    /// <summary>An empty snapshot at version zero.</summary>
    public static RouteConfigSnapshot Empty { get; } =
        new([], [], 0);

    /// <summary>Determines whether this snapshot's routes and clusters equal the given content, ignoring version.</summary>
    /// <param name="routes">Candidate routes.</param>
    /// <param name="clusters">Candidate clusters.</param>
    /// <returns><see langword="true"/> when routes and clusters are element-wise equal.</returns>
    public bool HasSameContent(ImmutableArray<RouteRule> routes, ImmutableArray<Cluster> clusters)
    {
        return RoutesEqual(Routes, routes) && ClustersEqual(Clusters, clusters);
    }

    private static bool RoutesEqual(ImmutableArray<RouteRule> left, ImmutableArray<RouteRule> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            // RouteRule holds only scalar/record fields, so record value equality is content equality here.
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool ClustersEqual(ImmutableArray<Cluster> left, ImmutableArray<Cluster> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (!ClusterEqual(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ClusterEqual(Cluster left, Cluster right)
    {
        // Compare scalar/record parts via record equality, then endpoints element-wise (ImmutableArray
        // would otherwise compare by reference).
        if (left.Id != right.Id
            || left.LoadBalancingPolicy != right.LoadBalancingPolicy
            || left.HealthCheck != right.HealthCheck
            || left.Endpoints.Length != right.Endpoints.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Endpoints.Length; i++)
        {
            if (left.Endpoints[i] != right.Endpoints[i])
            {
                return false;
            }
        }

        return true;
    }
}
