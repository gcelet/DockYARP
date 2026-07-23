namespace DockYarp.Security.Tests;

using AwesomeAssertions;

using DockYarp.Core.Configuration;
using DockYarp.Core.Models;
using DockYarp.Core.Stores;

using Microsoft.AspNetCore.Http;

/// <summary>Tests for <see cref="RouteLookup"/> per-request resolution caching.</summary>
public sealed class RouteLookupTests
{
    /// <summary>Once resolved, the route observed for a request is stable even if the store changes.</summary>
    [Test]
    public void ResolvedRouteIsCachedForTheRequest()
    {
        RouteConfigStore store = SecurityTestHelpers.StoreWith(
            new RouteRule { HostPattern = "app.local", ClusterId = "app" });
        RouteLookup lookup = new(store, new RoutingOptions());
        DefaultHttpContext context = SecurityTestHelpers.Context("http", "app.local", "/");

        lookup.TryGetRoute(context, out RouteRule? first).Should().BeTrue();

        store.Apply([], []);

        lookup.TryGetRoute(context, out RouteRule? second).Should().BeTrue();
        second.Should().BeSameAs(first);
    }

    /// <summary>A no-match result is cached too (a later store change does not make it match).</summary>
    [Test]
    public void NoMatchIsCachedForTheRequest()
    {
        RouteConfigStore store = SecurityTestHelpers.StoreWith();
        RouteLookup lookup = new(store, new RoutingOptions());
        DefaultHttpContext context = SecurityTestHelpers.Context("http", "app.local", "/");

        lookup.TryGetRoute(context, out _).Should().BeFalse();

        store.Apply([new RouteRule { HostPattern = "app.local", ClusterId = "app" }], []);

        lookup.TryGetRoute(context, out _).Should().BeFalse();
    }
}
