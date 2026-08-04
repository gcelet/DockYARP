namespace DockYarp.Docker.Discovery;

using System;

/// <summary>Computes the debounce delay that coalesces a burst of Docker events into one reconcile.</summary>
public static class DebouncePolicy
{
    /// <summary>Computes how long to wait for a further event before reconciling the current burst.</summary>
    /// <param name="sinceBurstStart">Elapsed time since the first event of the current burst.</param>
    /// <param name="min">The quiet window to wait after the most recent event.</param>
    /// <param name="max">The hard cap on the total wait, measured from the first event of the burst.</param>
    /// <returns>The delay to wait; <see cref="TimeSpan.Zero"/> to flush (reconcile) now.</returns>
    /// <remarks>
    /// Recomputed right after each event is consumed, so <paramref name="min"/> is the quiet window measured
    /// from the most recent event (each event extends it) while <c><paramref name="max"/> - sinceBurstStart</c>
    /// guarantees an unsettled burst is still flushed at the cap. A zero <paramref name="min"/> flushes on
    /// every event (debouncing disabled).
    /// </remarks>
    public static TimeSpan ComputeFlushDelay(TimeSpan sinceBurstStart, TimeSpan min, TimeSpan max)
    {
        TimeSpan capRemaining = max - sinceBurstStart;
        if (capRemaining <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return capRemaining < min ? capRemaining : min;
    }
}
