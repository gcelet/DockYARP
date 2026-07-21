namespace DockYarp.Tls;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using DockYarp.Core.Interfaces;
using DockYarp.Core.Models;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Acquires and renews certificates for hosts declaring TLS metadata.</summary>
/// <remarks>Reconciles on start and on a timer; each pass provisions missing certificates and renews
/// those near expiry via <see cref="IAcmeClient"/>.</remarks>
/// <param name="store">The routing store (source of desired hosts).</param>
/// <param name="certificates">The certificate store.</param>
/// <param name="acme">The ACME client.</param>
/// <param name="options">TLS options.</param>
/// <param name="logger">Logger.</param>
public sealed class CertificateProvisioningService(
    IRouteConfigStore store,
    ICertificateStore certificates,
    IAcmeClient acme,
    TlsOptions options,
    ILogger<CertificateProvisioningService> logger) : BackgroundService
{
    /// <summary>Performs one provisioning/renewal pass.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the pass finishes.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "One host's provisioning failure must be logged and must not stop the others or crash the service.")]
    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        RouteConfigSnapshot snapshot = store.Current;
        foreach (DesiredCertificate desired in TlsDomains.Desired(snapshot))
        {
            if (!NeedsCertificate(desired.Host))
            {
                continue;
            }

            try
            {
                X509Certificate2 certificate =
                    await acme.RequestCertificateAsync(desired.Host, desired.Email, cancellationToken).ConfigureAwait(false);
                certificates.Save(desired.Host, certificate);
                TlsLog.CertificateProvisioned(logger, desired.Host);
            }
            catch (Exception exception)
            {
                TlsLog.ProvisioningFailed(logger, desired.Host, exception);
            }
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ReconcileAsync(stoppingToken).ConfigureAwait(false);

        using PeriodicTimer timer = new(options.CheckInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await ReconcileAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested.
        }
    }

    private bool NeedsCertificate(string host)
    {
        X509Certificate2? existing = certificates.Find(host);
        if (existing is null)
        {
            return true;
        }

        return existing.NotAfter.ToUniversalTime() - DateTime.UtcNow <= options.RenewBeforeExpiry;
    }
}
