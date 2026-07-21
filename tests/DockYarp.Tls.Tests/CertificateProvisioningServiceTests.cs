namespace DockYarp.Tls.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Core.Models;
using DockYarp.Core.Stores;
using DockYarp.Tls;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Tests for <see cref="CertificateProvisioningService"/> using a fake ACME client.</summary>
public sealed class CertificateProvisioningServiceTests
{
    /// <summary>A host declaring TLS metadata and no certificate gets one provisioned and stored.</summary>
    [Test]
    public async Task AcquiresMissingCertificate()
    {
        RouteConfigStore routes = StoreWithTlsHost();
        FakeCertificateStore certificates = new();
        FakeAcmeClient acme = new();
        CertificateProvisioningService service = Service(routes, certificates, acme, new TlsOptions());

        await service.ReconcileAsync(CancellationToken.None);

        certificates.Find("app.local").Should().NotBeNull();
        acme.RequestCount("app.local").Should().Be(1);
    }

    /// <summary>A certificate within the renewal margin is renewed.</summary>
    [Test]
    public async Task RenewsCertificateNearExpiry()
    {
        RouteConfigStore routes = StoreWithTlsHost();
        FakeCertificateStore certificates = new();
        certificates.Save("app.local", DefaultCertificateFactory.CreateSelfSigned("app.local"));
        FakeAcmeClient acme = new();

        // A very large margin forces the existing (1-year) certificate to be treated as near expiry.
        CertificateProvisioningService service =
            Service(routes, certificates, acme, new TlsOptions { RenewBeforeExpiry = TimeSpan.FromDays(500) });

        await service.ReconcileAsync(CancellationToken.None);

        acme.RequestCount("app.local").Should().Be(1);
    }

    /// <summary>A host without TLS metadata is not provisioned.</summary>
    [Test]
    public async Task IgnoresHostsWithoutTlsMetadata()
    {
        RouteConfigStore routes = new();
        routes.Apply([new RouteRule { HostPattern = "open.local", ClusterId = "open" }], []);
        FakeCertificateStore certificates = new();
        FakeAcmeClient acme = new();
        CertificateProvisioningService service = Service(routes, certificates, acme, new TlsOptions());

        await service.ReconcileAsync(CancellationToken.None);

        certificates.Find("open.local").Should().BeNull();
    }

    private static RouteConfigStore StoreWithTlsHost()
    {
        RouteConfigStore routes = new();
        routes.Apply(
            [
                new RouteRule
                {
                    HostPattern = "app.local",
                    ClusterId = "app",
                    Tls = new HostTlsMetadata { CertificateHost = "app.local", ContactEmail = "a@example.com" },
                },
            ],
            []);
        return routes;
    }

    private static CertificateProvisioningService Service(
        RouteConfigStore routes,
        FakeCertificateStore certificates,
        FakeAcmeClient acme,
        TlsOptions options) =>
        new(routes, certificates, acme, options, NullLogger<CertificateProvisioningService>.Instance);
}
