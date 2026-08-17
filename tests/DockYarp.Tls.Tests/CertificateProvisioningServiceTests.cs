namespace DockYarp.Tls.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Core.Models;
using DockYarp.Core.Stores;
using DockYarp.Tls;

using Microsoft.Extensions.Logging;
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
        certificates.Save("app.local", new LoadedCertificate(DefaultCertificateFactory.CreateSelfSigned("app.local"), []));
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

    /// <summary>A host that fails once then succeeds logs the transient failure at Warning (not Error) and is provisioned.</summary>
    [Test]
    public async Task TransientFailureIsLoggedAsWarning()
    {
        RouteConfigStore routes = StoreWithTlsHost();
        FakeCertificateStore certificates = new();
        ScriptedAcmeClient acme = new(1); // attempt 1 fails, attempt 2 succeeds
        CapturingLogger logger = new();
        CertificateProvisioningService service = Service(routes, certificates, acme, new TlsOptions(), logger);

        await service.ReconcileAsync(CancellationToken.None); // fails (transient)
        await service.ReconcileAsync(CancellationToken.None); // succeeds

        certificates.Find("app.local").Should().NotBeNull();
        logger.Entries.Should().Contain((LogLevel.Warning, 5));
        logger.Entries.Should().NotContain(entry => entry.Level == LogLevel.Error);
    }

    /// <summary>A host that keeps failing escalates to Error once the transient threshold is exceeded.</summary>
    [Test]
    public async Task PersistentFailureEscalatesToError()
    {
        RouteConfigStore routes = StoreWithTlsHost();
        FakeCertificateStore certificates = new();
        FailingAcmeClient acme = new();
        CapturingLogger logger = new();
        CertificateProvisioningService service = Service(routes, certificates, acme, new TlsOptions(), logger);

        // Threshold is 2: the first two consecutive failures are Warnings, the third escalates to Error.
        await service.ReconcileAsync(CancellationToken.None);
        await service.ReconcileAsync(CancellationToken.None);
        await service.ReconcileAsync(CancellationToken.None);

        logger.Entries.Count(entry => entry == (LogLevel.Warning, 5)).Should().Be(2);
        logger.Entries.Should().Contain((LogLevel.Error, 2));
    }

    /// <summary>A success resets the counter, so a later failure is transient (Warning) again, not Error.</summary>
    [Test]
    public async Task SuccessResetsTheConsecutiveFailureCounter()
    {
        RouteConfigStore routes = StoreWithTlsHost();
        FakeCertificateStore certificates = new();
        ScriptedAcmeClient acme = new(1, 3); // fail, succeed, fail
        CapturingLogger logger = new();

        // A large renewal margin keeps the host "due" every pass, so it is re-attempted even after a success.
        CertificateProvisioningService service =
            Service(routes, certificates, acme, new TlsOptions { RenewBeforeExpiry = TimeSpan.FromDays(500) }, logger);

        await service.ReconcileAsync(CancellationToken.None); // fail  → Warning (attempt 1)
        await service.ReconcileAsync(CancellationToken.None); // succeed → counter cleared
        await service.ReconcileAsync(CancellationToken.None); // fail  → Warning again (attempt 1), not Error

        logger.Entries.Count(entry => entry == (LogLevel.Warning, 5)).Should().Be(2);
        logger.Entries.Should().NotContain(entry => entry.Level == LogLevel.Error);
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
        Service(routes, certificates, acme, options, NullLogger<CertificateProvisioningService>.Instance);

    private static CertificateProvisioningService Service(
        RouteConfigStore routes,
        FakeCertificateStore certificates,
        IAcmeClient acme,
        TlsOptions options,
        ILogger<CertificateProvisioningService> logger) =>
        new(routes, certificates, acme, new NoReservedCertificateHosts(), options, logger);

    /// <summary>Captures the level and event id of each log entry.</summary>
    private sealed class CapturingLogger : ILogger<CertificateProvisioningService>
    {
        public List<(LogLevel Level, int EventId)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, eventId.Id));
    }

    /// <summary>An ACME client that fails on the given 1-based attempt numbers and succeeds on the others.</summary>
    private sealed class ScriptedAcmeClient(params int[] failingAttempts) : IAcmeClient
    {
        private readonly HashSet<int> failing = [.. failingAttempts];
        private int attempts;

        public Task<LoadedCertificate> RequestCertificateAsync(string host, string? email, CancellationToken cancellationToken)
        {
            int attempt = Interlocked.Increment(ref attempts);
            return failing.Contains(attempt)
                ? Task.FromException<LoadedCertificate>(new TimeoutException("Timed out waiting for ACME authorization."))
                : Task.FromResult(new LoadedCertificate(DefaultCertificateFactory.CreateSelfSigned(host), []));
        }
    }

    /// <summary>An ACME client that always fails with a transient-looking timeout.</summary>
    private sealed class FailingAcmeClient : IAcmeClient
    {
        public Task<LoadedCertificate> RequestCertificateAsync(string host, string? email, CancellationToken cancellationToken) =>
            Task.FromException<LoadedCertificate>(new TimeoutException("Timed out waiting for ACME authorization."));
    }

    /// <summary>A fake ACME client where each of two hosts waits for the other to start, so both requests
    /// complete only when provisioning runs them concurrently.</summary>
    private sealed class RendezvousAcmeClient(string firstHost) : IAcmeClient
    {
        private readonly TaskCompletionSource firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource secondStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<LoadedCertificate> RequestCertificateAsync(string host, string? email, CancellationToken cancellationToken)
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

            return new LoadedCertificate(DefaultCertificateFactory.CreateSelfSigned(host), []);
        }
    }
}
