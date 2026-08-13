namespace DockYarp.Tls.Tests;

using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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

    /// <summary>Two hosts whose ACME requests each wait for the other to start are both provisioned — proving
    /// provisioning runs concurrently (a sequential pass would leave the first host waiting and failing).</summary>
    [Test]
    public async Task ProvisionsHostsConcurrently()
    {
        RouteConfigStore routes = StoreWithTlsHosts("a.local", "b.local");
        FakeCertificateStore certificates = new();
        RendezvousAcmeClient acme = new("a.local");
        CertificateProvisioningService service = Service(routes, certificates, acme, new TlsOptions());

        await service.ReconcileAsync(CancellationToken.None);

        certificates.Find("a.local").Should().NotBeNull();
        certificates.Find("b.local").Should().NotBeNull();
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

    private static RouteConfigStore StoreWithTlsHosts(params string[] hosts)
    {
        RouteConfigStore routes = new();
        routes.Apply(
            [.. hosts.Select(host => new RouteRule
            {
                HostPattern = host,
                ClusterId = host,
                Tls = new HostTlsMetadata { CertificateHost = host, ContactEmail = "a@example.com" },
            })],
            []);
        return routes;
    }

    private static CertificateProvisioningService Service(
        RouteConfigStore routes,
        FakeCertificateStore certificates,
        IAcmeClient acme,
        TlsOptions options) =>
        new(routes, certificates, acme, new NoReservedCertificateHosts(), options, NullLogger<CertificateProvisioningService>.Instance);

    /// <summary>A fake ACME client where each of two hosts waits for the other to start, so both requests
    /// complete only when provisioning runs them concurrently.</summary>
    private sealed class RendezvousAcmeClient(string firstHost) : IAcmeClient
    {
        private readonly TaskCompletionSource firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource secondStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<X509Certificate2> RequestCertificateAsync(string host, string? email, CancellationToken cancellationToken)
        {
            // Each host signals its arrival and waits for the other's; a generous timeout keeps a sequential
            // regression from hanging the test (the blocked host times out and fails instead of both passing).
            if (string.Equals(host, firstHost, StringComparison.OrdinalIgnoreCase))
            {
                firstStarted.TrySetResult();
                await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            else
            {
                secondStarted.TrySetResult();
                await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }

            return DefaultCertificateFactory.CreateSelfSigned(host);
        }
    }
}
