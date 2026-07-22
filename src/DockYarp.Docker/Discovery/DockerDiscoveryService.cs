namespace DockYarp.Docker.Discovery;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

using DockYarp.Docker.Models;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Background service that keeps the routing store in sync with Docker.</summary>
/// <remarks>
/// On start and after every reconnect it performs a full reconciliation, then applies one reconciliation
/// per lifecycle event so start/stop/die/update converge. Connection failures trigger capped exponential
/// backoff rather than crashing the host.
/// </remarks>
/// <param name="source">The container source (event stream).</param>
/// <param name="reconciler">The reconciler that publishes state.</param>
/// <param name="options">Discovery options (backoff bounds).</param>
/// <param name="health">Shared connection-health state for observability.</param>
/// <param name="logger">Logger for connection-state transitions.</param>
public sealed class DockerDiscoveryService(
    IContainerSource source,
    DiscoveryReconciler reconciler,
    DockerDiscoveryOptions options,
    DiscoveryHealthState health,
    ILogger<DockerDiscoveryService> logger) : BackgroundService
{
    /// <inheritdoc />
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Resilience loop: any failure talking to Docker must trigger reconnect/backoff, never crash the host.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int attempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await reconciler.ReconcileAsync(stoppingToken).ConfigureAwait(false);
                attempt = 0;
                health.MarkConnected();
                DiscoveryLog.Connected(logger);

                await foreach (ContainerLifecycleEvent lifecycleEvent in
                    source.WatchAsync(stoppingToken).ConfigureAwait(false))
                {
                    _ = lifecycleEvent;
                    await reconciler.ReconcileAsync(stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                await DelayBeforeReconnectAsync(++attempt, exception, stoppingToken).ConfigureAwait(false);
                continue;
            }

            // The event stream ended without cancellation: treat as a disconnect and reconnect.
            await DelayBeforeReconnectAsync(++attempt, exception: null, stoppingToken).ConfigureAwait(false);
        }
    }

    private Task DelayBeforeReconnectAsync(int attempt, Exception? exception, CancellationToken cancellationToken)
    {
        health.MarkDisconnected();
        TimeSpan delay = BackoffPolicy.Compute(attempt, options.InitialReconnectDelay, options.MaxReconnectDelay);
        DiscoveryLog.Reconnecting(logger, attempt, delay, exception);
        return Task.Delay(delay, cancellationToken);
    }
}
