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

    /// <summary>A route whose host is a bare IPv4 address matches a request to that IP (raw-IP vhost).</summary>
    [Test]
    public void RawIpv4HostMatchedExactly()
    {
        RouteMatcher matcher = new(
        [
            new RouteRule { HostPattern = "192.0.2.10", ClusterId = "ip" },
        ]);

        bool matched = matcher.TryMatch("192.0.2.10", "/", out RouteRule? route);

        matched.Should().BeTrue();
        route!.ClusterId.Should().Be("ip");
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

    /// <summary>A wildcard matches a nested (multi-level) subdomain, not just a single level.</summary>
    [Test]
    public void WildcardMatchesNestedSubdomain()
    {
        RouteMatcher matcher = new([new RouteRule { HostPattern = "*.local", ClusterId = "wild" }]);

        bool matched = matcher.TryMatch("a.b.local", "/", out RouteRule? route);

        matched.Should().BeTrue();
        route!.ClusterId.Should().Be("wild");
    }

    /// <summary>A trailing-wildcard route matches any host beginning with the prefix.</summary>
    [Test]
    public void TrailingWildcardMatchesSuffix()
    {
        RouteMatcher matcher = new([new RouteRule { HostPattern = "app.*", ClusterId = "trailing" }]);

        bool matched = matcher.TryMatch("app.example.com", "/", out RouteRule? route);

        matched.Should().BeTrue();
        route!.ClusterId.Should().Be("trailing");
    }

    /// <summary>An exact host wins over a matching trailing wildcard.</summary>
    [Test]
    public void ExactHostWinsOverTrailingWildcard()
    {
        RouteMatcher matcher = new(
        [
            new RouteRule { HostPattern = "app.local", ClusterId = "exact" },
            new RouteRule { HostPattern = "app.*", ClusterId = "trailing" },
        ]);

        matcher.TryMatch("app.local", "/", out RouteRule? route).Should().BeTrue();
        route!.ClusterId.Should().Be("exact");
    }

    /// <summary>A leading wildcard wins over a trailing wildcard when both match.</summary>
    [Test]
    public void LeadingWildcardWinsOverTrailingWildcard()
    {
        RouteMatcher matcher = new(
        [
            new RouteRule { HostPattern = "*.local", ClusterId = "leading" },
            new RouteRule { HostPattern = "app.*", ClusterId = "trailing" },
        ]);

        matcher.TryMatch("app.local", "/", out RouteRule? route).Should().BeTrue();
        route!.ClusterId.Should().Be("leading");
    }

    /// <summary>An exact host still wins over a matching multi-level wildcard.</summary>
    [Test]
    public void ExactHostWinsOverNestedWildcard()
    {
        RouteMatcher matcher = new(
        [
            new RouteRule { HostPattern = "a.b.local", ClusterId = "exact" },
            new RouteRule { HostPattern = "*.local", ClusterId = "wild" },
        ]);

        bool matched = matcher.TryMatch("a.b.local", "/", out RouteRule? route);

        matched.Should().BeTrue();
        route!.ClusterId.Should().Be("exact");
    }

    /// <summary>A configured default host serves requests whose host matches no other route.</summary>
    [Test]
    public void DefaultHostServesUnknownHosts()
    {
        RouteMatcher matcher = new(
            [new RouteRule { HostPattern = "app.local", ClusterId = "app" }],
            defaultHost: "app.local");

        bool matched = matcher.TryMatch("unknown.example", "/", out RouteRule? route);

        matched.Should().BeTrue();
        route!.ClusterId.Should().Be("app");
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
