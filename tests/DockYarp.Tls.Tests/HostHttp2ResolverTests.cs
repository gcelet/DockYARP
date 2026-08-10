namespace DockYarp.Tls.Tests;

using AwesomeAssertions;

using DockYarp.Core.Models;
using DockYarp.Core.Stores;
using DockYarp.Tls;

/// <summary>Tests for <see cref="HostHttp2Resolver"/>.</summary>
public sealed class HostHttp2ResolverTests
{
    /// <summary>The per-host HTTP/2 toggle is returned for a matching host.</summary>
    [Test]
    public void ResolvesToggleForMatchingHost()
    {
        RouteConfigStore store = new();
        store.Apply(
            [Route("off.local", http2: false), Route("on.local", http2: true)],
            []);

        HostHttp2Resolver.Resolve(store.Current, "off.local").Should().BeFalse();
        HostHttp2Resolver.Resolve(store.Current, "on.local").Should().BeTrue();
    }

    /// <summary>A host with no toggle, or an unmatched host, resolves to null.</summary>
    [Test]
    public void ResolvesNullWhenAbsent()
    {
        RouteConfigStore store = new();
        store.Apply(
            [Route("app.local", http2: null)],
            []);

        HostHttp2Resolver.Resolve(store.Current, "app.local").Should().BeNull();
        HostHttp2Resolver.Resolve(store.Current, "other.local").Should().BeNull();
    }

    private static RouteRule Route(string host, bool? http2) =>
        new()
        {
            HostPattern = host,
            ClusterId = host,
            Tls = new HostTlsMetadata { CertificateHost = host, Http2Enabled = http2 },
        };
}
