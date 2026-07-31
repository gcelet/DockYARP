namespace DockYarp.Core.Tests;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using AwesomeAssertions;

using DockYarp.Core.Configuration;
using DockYarp.Core.Models;

/// <summary>Tests for <see cref="RouteOverrideApplier"/>.</summary>
public sealed class RouteOverrideApplierTests
{
    /// <summary>A host-specific override sets that host's response headers.</summary>
    [Test]
    public void PerHostHeadersAreApplied()
    {
        ConfigOverrides overrides = Overrides("app.local", Headers(("X-Frame-Options", "DENY")), defaultHeaders: null);

        RouteRule route = Apply("app.local", overrides);

        route.Transforms!.ResponseHeaders.Should().ContainKey("X-Frame-Options").WhoseValue.Should().Be("DENY");
    }

    /// <summary>The default override applies to a host with no specific override.</summary>
    [Test]
    public void DefaultAppliesToOtherHosts()
    {
        ConfigOverrides overrides = Overrides(
            "app.local", Headers(("X-Frame-Options", "DENY")), Headers(("X-Content-Type-Options", "nosniff")));

        RouteRule route = Apply("other.local", overrides);

        route.Transforms!.ResponseHeaders.Should().ContainKey("X-Content-Type-Options").WhoseValue.Should().Be("nosniff");
        route.Transforms.ResponseHeaders.Should().NotContainKey("X-Frame-Options");
    }

    /// <summary>A host-specific override wins over the default one.</summary>
    [Test]
    public void HostSpecificWinsOverDefault()
    {
        ConfigOverrides overrides = Overrides(
            "app.local", Headers(("X-Frame-Options", "DENY")), Headers(("X-Content-Type-Options", "nosniff")));

        RouteRule route = Apply("app.local", overrides);

        route.Transforms!.ResponseHeaders.Should().ContainKey("X-Frame-Options");
        route.Transforms.ResponseHeaders.Should().NotContainKey("X-Content-Type-Options");
    }

    /// <summary>Applying an override preserves existing (path) transforms.</summary>
    [Test]
    public void ExistingTransformsPreserved()
    {
        RouteRule input = new()
        {
            HostPattern = "app.local",
            ClusterId = "app",
            Transforms = new RouteTransforms { PathRemovePrefix = "/api" },
        };
        ConfigOverrides overrides = Overrides("app.local", Headers(("X-Frame-Options", "DENY")), defaultHeaders: null);

        RouteRule route = RouteOverrideApplier.Apply([input], overrides).Single();

        route.Transforms!.PathRemovePrefix.Should().Be("/api");
        route.Transforms.ResponseHeaders.Should().ContainKey("X-Frame-Options");
    }

    /// <summary>Empty overrides leave routes unchanged (no transforms added).</summary>
    [Test]
    public void EmptyOverridesLeaveRoutesUnchanged()
    {
        RouteRule input = new() { HostPattern = "app.local", ClusterId = "app" };

        RouteRule route = RouteOverrideApplier.Apply([input], ConfigOverrides.Empty).Single();

        route.Transforms.Should().BeNull();
    }

    private static RouteRule Apply(string host, ConfigOverrides overrides)
    {
        RouteRule input = new() { HostPattern = host, ClusterId = "app" };
        return RouteOverrideApplier.Apply([input], overrides).Single();
    }

    private static IReadOnlyDictionary<string, string> Headers(params (string Name, string Value)[] entries)
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string Name, string Value) entry in entries)
        {
            headers[entry.Name] = entry.Value;
        }

        return headers;
    }

    private static ConfigOverrides Overrides(
        string host,
        IReadOnlyDictionary<string, string> headers,
        IReadOnlyDictionary<string, string>? defaultHeaders) =>
        new()
        {
            ResponseHeadersByHost = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                [host] = headers,
            },
            DefaultResponseHeaders = defaultHeaders,
        };
}
