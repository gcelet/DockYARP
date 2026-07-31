namespace DockYarp.Core.Configuration;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using DockYarp.Core.Models;

/// <summary>Applies structured per-host / global overrides to the merged routes.</summary>
/// <remarks>Pure and side-effect free so it can be unit tested without the discovery or static pipeline.</remarks>
public static class RouteOverrideApplier
{
    /// <summary>Layers the overrides onto the routes, populating each route's response-header transforms.</summary>
    /// <param name="routes">The merged routes.</param>
    /// <param name="overrides">The configured overrides.</param>
    /// <returns>The routes with any matching override's response headers applied.</returns>
    public static ImmutableArray<RouteRule> Apply(ImmutableArray<RouteRule> routes, ConfigOverrides overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        if (routes.IsDefaultOrEmpty)
        {
            return routes;
        }

        ImmutableArray<RouteRule>.Builder result = ImmutableArray.CreateBuilder<RouteRule>(routes.Length);
        foreach (RouteRule route in routes)
        {
            result.Add(ApplyToRoute(route, overrides));
        }

        return result.ToImmutable();
    }

    private static RouteRule ApplyToRoute(RouteRule route, ConfigOverrides overrides)
    {
        // A host-specific override wins; otherwise the global (default) override applies.
        IReadOnlyDictionary<string, string>? headers =
            overrides.ResponseHeadersByHost.TryGetValue(route.HostPattern, out IReadOnlyDictionary<string, string>? perHost)
                ? perHost
                : overrides.DefaultResponseHeaders;

        if (headers is not { Count: > 0 })
        {
            return route;
        }

        RouteTransforms transforms = (route.Transforms ?? new RouteTransforms()) with { ResponseHeaders = headers };
        return route with { Transforms = transforms };
    }
}
