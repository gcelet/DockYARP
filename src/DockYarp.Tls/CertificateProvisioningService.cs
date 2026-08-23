namespace DockYarp.Tls;

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
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
/// <param name="reserved">Non-route hosts to provision as well (for example the dedicated admin host).</param>
/// <param name="options">TLS options.</param>
/// <param name="logger">Logger.</param>
public sealed class CertificateProvisioningService(
    IRouteConfigStore store,
    ICertificateStore certificates,
    IAcmeClient acme,
    IReservedCertificateHosts reserved,
    TlsOptions options,
    ILogger<CertificateProvisioningService> logger) : BackgroundService
{
    // Bounded so concurrent ACME orders do not trip the authority's rate limits.
    private const int MaxConcurrentProvisions = 8;

    // The first consecutive per-host failures are treated as transient (Warning, no stack trace) — this silences the
    // common startup validation race, which the next pass resolves — before a persistent failure escalates to Error.
    private const int TransientFailureThreshold = 2;

    // Per-host count of consecutive provisioning failures; reset on success. Concurrent because a pass provisions
    // hosts in parallel (distinct keys per pass, but the map itself must be thread-safe).
    private readonly ConcurrentDictionary<string, int> consecutiveFailures = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Performs one provisioning/renewal pass.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the pass finishes.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "One host's provisioning failure must be logged and must not stop the others or crash the service.")]
    public Task ReconcileAsync(CancellationToken cancellationToken)
    {
        RouteConfigSnapshot snapshot = store.Current;
        ParallelOptions parallelOptions = new()
        {
            MaxDegreeOfParallelism = MaxConcurrentProvisions,
            CancellationToken = cancellationToken,
        };

        // Provision hosts concurrently (bounded) so one host's slow/failing ACME validation does not block the
        // others. Per-host failures stay isolated; cancellation propagates so a shutdown stops the pass.
        return Parallel.ForEachAsync(TlsDomains.Desired(snapshot, reserved.Reserved), parallelOptions, async (desired, token) =>
        {
            // A wildcard identifier (*.example.com) is stored under its parent domain (example.com),
            // matching SniCertificateSelector's existing wildcard-fallback lookup.
            string storageKey = desired.Host.StartsWith("*.", StringComparison.Ordinal)
                ? desired.Host[2..]
                : desired.Host;

            if (!NeedsCertificate(storageKey))
            {
                return;
            }

            try
            {
                LoadedCertificate certificate = await acme
                    .RequestCertificateAsync(desired.Host, desired.Email, desired.ChallengeType, token)
                    .ConfigureAwait(false);

                certificates.Save(storageKey, certificate);
                consecutiveFailures.TryRemove(desired.Host, out _);
                TlsLog.CertificateProvisioned(logger, desired.Host);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                LogProvisioningFailure(desired.Host, exception);
            }
        });
    }

    // A per-host failure the reconcile loop retries is logged as transient (Warning, no stack trace) until it persists
    // past the threshold, at which point it escalates to Error with the exception (a genuine, actionable failure).
    private void LogProvisioningFailure(string host, Exception exception)
    {
        int attempt = consecutiveFailures.AddOrUpdate(host, 1, static (_, previous) => previous + 1);
        if (attempt <= TransientFailureThreshold)
        {
            TlsLog.ProvisioningRetrying(logger, host, attempt, exception.Message);
        }
        else
        {
            TlsLog.ProvisioningFailed(logger, host, exception);
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
        LoadedCertificate? existing = certificates.Find(host);
        if (existing is null)
        {
            return true;
        }

        return existing.Leaf.NotAfter.ToUniversalTime() - DateTime.UtcNow <= options.RenewBeforeExpiry;
    }
}
