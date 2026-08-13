namespace DockYarp.Tls.Tests;

using System.Collections.Generic;
using System.Linq;

using AwesomeAssertions;

using DockYarp.Core.Models;
using DockYarp.Tls;

/// <summary>Tests for <see cref="TlsDomains"/>.</summary>
public sealed class TlsDomainsTests
{
    /// <summary>Only routes with TLS metadata contribute desired certificates.</summary>
    [Test]
    public void DerivesHostsWithTlsMetadata()
    {
        RouteConfigSnapshot snapshot = new(
            [
                new RouteRule
                {
                    HostPattern = "app.local",
                    ClusterId = "app",
                    Tls = new HostTlsMetadata { CertificateHost = "app.local", ContactEmail = "a@example.com" },
                },
                new RouteRule { HostPattern = "open.local", ClusterId = "open" },
            ],
            [],
            1);

        IReadOnlyList<DesiredCertificate> desired = TlsDomains.Desired(snapshot);

        desired.Should().ContainSingle();
        desired.Single().Host.Should().Be("app.local");
        desired.Single().Email.Should().Be("a@example.com");
    }

    /// <summary>A host whose HTTPS method is nohttps is excluded from provisioning.</summary>
    [Test]
    public void NoHttpsHostIsNotProvisioned()
    {
        RouteConfigSnapshot snapshot = new(
            [
                new RouteRule
                {
                    HostPattern = "app.local",
                    ClusterId = "app",
                    Tls = new HostTlsMetadata { CertificateHost = "app.local", Method = HttpsMethod.NoHttps },
                },
            ],
            [],
            1);

        TlsDomains.Desired(snapshot).Should().BeEmpty();
    }

    /// <summary>A host pinned to a CERT_NAME shared certificate is not ACME-provisioned.</summary>
    [Test]
    public void CertNameHostIsNotProvisioned()
    {
        RouteConfigSnapshot snapshot = new(
            [
                new RouteRule
                {
                    HostPattern = "app.local",
                    ClusterId = "app",
                    Tls = new HostTlsMetadata { CertificateHost = "app.local", CertificateName = "shared" },
                },
            ],
            [],
            1);

        TlsDomains.Desired(snapshot).Should().BeEmpty();
    }

    /// <summary>A reserved host that is not a route is appended to the desired set.</summary>
    [Test]
    public void ReservedHostIsAppended()
    {
        RouteConfigSnapshot snapshot = new(
            [
                new RouteRule
                {
                    HostPattern = "app.local",
                    ClusterId = "app",
                    Tls = new HostTlsMetadata { CertificateHost = "app.local", ContactEmail = "a@example.com" },
                },
            ],
            [],
            1);

        IReadOnlyList<DesiredCertificate> desired =
            TlsDomains.Desired(snapshot, [new DesiredCertificate("admin.local", "admin@example.com")]);

        desired.Select(entry => entry.Host).Should().BeEquivalentTo("app.local", "admin.local");
        desired.Single(entry => entry.Host == "admin.local").Email.Should().Be("admin@example.com");
    }

    /// <summary>A reserved host equal to a route host does not duplicate the entry (the route wins).</summary>
    [Test]
    public void ReservedHostDuplicatingARouteIsNotAddedTwice()
    {
        RouteConfigSnapshot snapshot = new(
            [
                new RouteRule
                {
                    HostPattern = "app.local",
                    ClusterId = "app",
                    Tls = new HostTlsMetadata { CertificateHost = "app.local", ContactEmail = "a@example.com" },
                },
            ],
            [],
            1);

        // Same host as the route (case-insensitively) → deduped; the route's email is kept.
        IReadOnlyList<DesiredCertificate> desired =
            TlsDomains.Desired(snapshot, [new DesiredCertificate("APP.local", "other@example.com")]);

        desired.Should().ContainSingle();
        desired.Single().Host.Should().Be("app.local");
        desired.Single().Email.Should().Be("a@example.com");
    }

    /// <summary>An empty reserved list yields exactly the route-derived hosts.</summary>
    [Test]
    public void EmptyReservedEqualsRouteOnly()
    {
        RouteConfigSnapshot snapshot = new(
            [
                new RouteRule
                {
                    HostPattern = "app.local",
                    ClusterId = "app",
                    Tls = new HostTlsMetadata { CertificateHost = "app.local", ContactEmail = "a@example.com" },
                },
            ],
            [],
            1);

        TlsDomains.Desired(snapshot, []).Should().BeEquivalentTo(TlsDomains.Desired(snapshot));
    }
}
