namespace DockYarp.Docker.Discovery;

using System;

/// <summary>Computes capped exponential reconnect delays.</summary>
public static class BackoffPolicy
{
    /// <summary>Computes the delay for a reconnect attempt.</summary>
    /// <param name="attempt">The 1-based attempt number.</param>
    /// <param name="initial">The base delay for the first attempt.</param>
    /// <param name="max">The maximum delay (ceiling).</param>
    /// <returns>The delay to wait before the next attempt.</returns>
    public static TimeSpan Compute(int attempt, TimeSpan initial, TimeSpan max)
    {
        int exponent = Math.Max(0, attempt - 1);
        double factor = Math.Pow(2, Math.Min(exponent, 30));
        double milliseconds = Math.Min(initial.TotalMilliseconds * factor, max.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
