namespace DockYarp.Tls.Tests;

using AwesomeAssertions;

using DockYarp.Core.Models;
using DockYarp.Tls;

/// <summary>Tests for <see cref="CertificateNameResolver"/>.</summary>
public sealed class CertificateNameResolverTests
{
    /// <summary>A host whose route pins a CERT_NAME resolves to that name.</summary>
    [Test]
    public void ResolvesPinnedCertificateName()
    {
        RouteConfigSnapshot snapshot = Snapshot(Route("app.local", "shared"));

        CertificateNameResolver.Resolve(snapshot, "app.local").Should().Be("shared");
    }

    /// <summary>A wildcard host pattern's CERT_NAME applies to its subdomains.</summary>
    [Test]
    public void ResolvesThroughWildcardHost()
    {
        RouteConfigSnapshot snapshot = Snapshot(Route("*.example.com", "shared"));

        CertificateNameResolver.Resolve(snapshot, "foo.example.com").Should().Be("shared");
    }

    /// <summary>A host with no CERT_NAME pin resolves to null.</summary>
    [Test]
    public void ResolvesNullWhenUnpinned()
    {
        RouteConfigSnapshot snapshot = Snapshot(
            new RouteRule
            {
                HostPattern = "app.local",
                ClusterId = "app",
                Tls = new HostTlsMetadata { CertificateHost = "app.local" },
            });

        CertificateNameResolver.Resolve(snapshot, "app.local").Should().BeNull();
        CertificateNameResolver.Resolve(snapshot, "other.local").Should().BeNull();
    }

    private static RouteRule Route(string hostPattern, string certName) =>
        new()
        {
            HostPattern = hostPattern,
            ClusterId = hostPattern,
            Tls = new HostTlsMetadata { CertificateHost = hostPattern, CertificateName = certName },
        };

    private static RouteConfigSnapshot Snapshot(params RouteRule[] routes) => new([.. routes], [], 1);
}
