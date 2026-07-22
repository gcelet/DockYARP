namespace DockYarp.Security;

using System.Diagnostics.CodeAnalysis;
using System.Threading;

using DockYarp.Core.Configuration;
using DockYarp.Core.Interfaces;
using DockYarp.Core.Models;
using DockYarp.Core.Routing;

/// <summary>Resolves the route matching a request, caching a matcher until the store version changes.</summary>
/// <param name="store">The route configuration store.</param>
/// <param name="routing">Routing options supplying the optional default host.</param>
public sealed class RouteLookup(IRouteConfigStore store, RoutingOptions routing)
{
    private readonly Lock gate = new();
    private RouteMatcher? matcher;
    private long version = -1;

    /// <summary>Attempts to find the route matching the host and path.</summary>
    /// <param name="host">Request host (without port).</param>
    /// <param name="path">Request path.</param>
    /// <param name="route">The matched route when found.</param>
    /// <returns><see langword="true"/> when a route matches.</returns>
    public bool TryMatch(string host, string path, [MaybeNullWhen(false)] out RouteRule route)
    {
        RouteConfigSnapshot snapshot = store.Current;
        RouteMatcher current;
        lock (gate)
        {
            if (matcher is null || version != snapshot.Version)
            {
                matcher = new RouteMatcher(snapshot.Routes, routing.DefaultHost);
                version = snapshot.Version;
            }

            current = matcher;
        }

        return current.TryMatch(host, path, out route);
    }
}
