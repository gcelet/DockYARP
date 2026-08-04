namespace DockYarp.Docker.Discovery;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

using DockYarp.Docker.Models;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Background service that keeps the routing store in sync with Docker.</summary>
/// <remarks>
/// On start and after every reconnect it performs a full reconciliation, then coalesces lifecycle events into
/// debounced reconciliations so a burst of start/stop/die/update changes converges in a single pass.
/// Connection failures trigger capped exponential backoff rather than crashing the host.
/// </remarks>
/// <param name="source">The container source (event stream).</param>
/// <param name="reconciler">The reconciler that publishes state.</param>
/// <param name="options">Discovery options (backoff bounds, debounce window).</param>
/// <param name="health">Shared connection-health state for observability.</param>
/// <param name="timeProvider">Clock driving the event debounce window.</param>
/// <param name="logger">Logger for connection-state transitions.</param>
public sealed class DockerDiscoveryService(
    IContainerSource source,
    DiscoveryReconciler reconciler,
    DockerDiscoveryOptions options,
    DiscoveryHealthState health,
    TimeProvider timeProvider,
    ILogger<DockerDiscoveryService> logger) : BackgroundService
{
    // A Docker read started to detect the end of a burst but not yet consumed when the debounce window
    // elapsed; the next burst consumes it as its first event so no event is dropped or double-counted.
    private Task<bool>? bufferedRead;

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

                await PumpEventsAsync(stoppingToken).ConfigureAwait(false);
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

    // Consumes the lifecycle event stream, coalescing each burst of events into a single reconciliation.
    private async Task PumpEventsAsync(CancellationToken cancellationToken)
    {
        bufferedRead = null; // a carried read from a prior connection belongs to a now-disposed enumerator
        await using IAsyncEnumerator<ContainerLifecycleEvent> events =
            source.WatchAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);

        Task<bool> read = ReadNextAsync(events);
        while (await read.ConfigureAwait(false))
        {
            // A burst starts with the event just produced; wait out the debounce window, then reconcile once.
            await CoalesceBurstAsync(events, cancellationToken).ConfigureAwait(false);
            await reconciler.ReconcileAsync(cancellationToken).ConfigureAwait(false);

            read = bufferedRead ?? ReadNextAsync(events);
            bufferedRead = null;
        }
    }

    // Awaits a single MoveNextAsync, materializing it as a Task so it can be raced against a delay and carried
    // across a flush (a ValueTask must be consumed only once).
    private static async Task<bool> ReadNextAsync(IAsyncEnumerator<ContainerLifecycleEvent> events) =>
        await events.MoveNextAsync().ConfigureAwait(false);

    // Waits out the debounce window for the current burst, folding in any events that arrive within it.
    private async Task CoalesceBurstAsync(
        IAsyncEnumerator<ContainerLifecycleEvent> events, CancellationToken cancellationToken)
    {
        long start = timeProvider.GetTimestamp();
        while (true)
        {
            TimeSpan wait = DebouncePolicy.ComputeFlushDelay(
                timeProvider.GetElapsedTime(start), options.ReconcileDebounceMin, options.ReconcileDebounceMax);
            if (wait <= TimeSpan.Zero)
            {
                return; // hard cap reached: flush now
            }

            Task<bool> read = ReadNextAsync(events);
            Task settled = await Task.WhenAny(read, Task.Delay(wait, timeProvider, cancellationToken))
                .ConfigureAwait(false);
            if (settled != read)
            {
                bufferedRead = read; // quiet window elapsed: flush, carry the pending read into the next burst
                return;
            }

            if (!await read.ConfigureAwait(false))
            {
                bufferedRead = read; // stream ended: flush, carry (false) so the pump loop exits
                return;
            }

            // A new event arrived within the window: coalesce it and extend (bounded by the cap).
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
