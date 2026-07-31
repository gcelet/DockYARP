namespace DockYarp.Tls.Tests;

using AwesomeAssertions;

using DockYarp.Core.Models;
using DockYarp.Core.Stores;
using DockYarp.Tls;

/// <summary>Tests for <see cref="HostSslPolicyResolver"/>.</summary>
public sealed class HostSslPolicyResolverTests
{
    /// <summary>The per-host SSL_POLICY is returned for a matching host.</summary>
    [Test]
    public void ResolvesPolicyForMatchingHost()
    {
        RouteConfigStore store = new();
        store.Apply(
            [Route("app.local", "Mozilla-Modern")],
            []);

        HostSslPolicyResolver.Resolve(store.Current, "app.local").Should().Be("Mozilla-Modern");
    }

    /// <summary>A host with no SSL_POLICY, or an unmatched host, resolves to null.</summary>
    [Test]
    public void ResolvesNullWhenAbsent()
    {
        RouteConfigStore store = new();
        store.Apply(
            [Route("app.local", policy: null)],
            []);

        HostSslPolicyResolver.Resolve(store.Current, "app.local").Should().BeNull();
        HostSslPolicyResolver.Resolve(store.Current, "other.local").Should().BeNull();
    }

    private static RouteRule Route(string host, string? policy) =>
        new()
        {
            HostPattern = host,
            ClusterId = host,
            Tls = new HostTlsMetadata { CertificateHost = host, SslPolicy = policy },
        };
}
