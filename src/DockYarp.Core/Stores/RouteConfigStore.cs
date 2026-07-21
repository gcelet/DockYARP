namespace DockYarp.Core.Stores;

using System;
using System.Collections.Immutable;
using System.Threading;

using DockYarp.Core.Interfaces;
using DockYarp.Core.Models;

/// <summary>Thread-safe, versioned route configuration store.</summary>
/// <remarks>
/// Reads are lock-free (a volatile reference read of an immutable snapshot). Writes are serialized by a
/// lock and swap the published reference atomically, so a reader either sees the whole old snapshot or the
/// whole new one. An update whose content is identical to the current snapshot is a no-op: the same
/// reference and version are kept so consumers are not churned.
/// </remarks>
public sealed class RouteConfigStore : IRouteConfigStore
{
    private readonly Lock writeLock = new();
    private RouteConfigSnapshot current = RouteConfigSnapshot.Empty;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public RouteConfigSnapshot Current => Volatile.Read(ref current);

    /// <inheritdoc />
    public RouteConfigSnapshot Apply(ImmutableArray<RouteRule> routes, ImmutableArray<Cluster> clusters)
    {
        RouteConfigSnapshot next;
        lock (writeLock)
        {
            RouteConfigSnapshot existing = current;
            if (existing.HasSameContent(routes, clusters))
            {
                return existing;
            }

            next = new RouteConfigSnapshot(routes, clusters, existing.Version + 1);
            Volatile.Write(ref current, next);
        }

        // Raised outside the lock so a slow observer cannot block other writers.
        OnChanged();
        return next;
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
