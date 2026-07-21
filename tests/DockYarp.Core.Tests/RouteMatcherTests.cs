namespace DockYarp.Core.Tests;

using System.Collections.Immutable;

using AwesomeAssertions;

using DockYarp.Core.Models;
using DockYarp.Core.Routing;

/// <summary>Tests for <see cref="RouteMatcher"/>.</summary>
public sealed class RouteMatcherTests
{
    /// <summary>An exact host match is preferred over a wildcard subdomain match.</summary>
    [Test]
    public void ExactHostPreferredOverWildcard()
    {
        RouteMatcher matcher = new(
        [
            new RouteRule { HostPattern = "app.local", ClusterId = "exact" },
            new RouteRule { HostPattern = "*.local", ClusterId = "wild" },
        ]);

        bool matched = matcher.TryMatch("app.local", "/", out RouteRule? route);

        matched.Should().BeTrue();
        route!.ClusterId.Should().Be("exact");
    }

    /// <summary>The longest matching path prefix wins among same-host routes.</summary>
    [Test]
    public void LongestPathPrefixWins()
    {
        RouteMatcher matcher = new(
        [
            new RouteRule { HostPattern = "app.local", PathPrefix = "/", ClusterId = "root" },
            new RouteRule { HostPattern = "app.local", PathPrefix = "/api", ClusterId = "api" },
        ]);

        bool matched = matcher.TryMatch("app.local", "/api/orders", out RouteRule? route);

        matched.Should().BeTrue();
        route!.ClusterId.Should().Be("api");
    }

    /// <summary>A wildcard subdomain route matches a matching host.</summary>
    [Test]
    public void WildcardMatchesSubdomain()
    {
        RouteMatcher matcher = new([new RouteRule { HostPattern = "*.local", ClusterId = "wild" }]);

        bool matched = matcher.TryMatch("app.local", "/", out RouteRule? route);

        matched.Should().BeTrue();
        route!.ClusterId.Should().Be("wild");
    }

    /// <summary>No route matches an unknown host.</summary>
    [Test]
    public void NoMatchForUnknownHost()
    {
        RouteMatcher matcher = new([new RouteRule { HostPattern = "app.local", ClusterId = "app" }]);

        bool matched = matcher.TryMatch("other.example", "/", out RouteRule? route);

        matched.Should().BeFalse();
        route.Should().BeNull();
    }
}
