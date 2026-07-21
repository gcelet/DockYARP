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
}
