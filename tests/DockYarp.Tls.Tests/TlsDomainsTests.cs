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
}
