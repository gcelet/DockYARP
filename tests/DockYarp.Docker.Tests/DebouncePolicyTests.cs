namespace DockYarp.Docker.Tests;

using System;

using AwesomeAssertions;

using DockYarp.Docker.Discovery;

/// <summary>Tests for <see cref="DebouncePolicy"/>.</summary>
public sealed class DebouncePolicyTests
{
    private static readonly TimeSpan Min = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan Max = TimeSpan.FromSeconds(2);

    /// <summary>At the start of a burst the full quiet window is waited.</summary>
    [Test]
    public void FreshBurstWaitsTheQuietWindow()
    {
        TimeSpan delay = DebouncePolicy.ComputeFlushDelay(TimeSpan.Zero, Min, Max);

        delay.Should().Be(Min);
    }

    /// <summary>Near the cap the wait is clamped to the remaining cap, not the full quiet window.</summary>
    [Test]
    public void NearTheCapReturnsTheRemainingCap()
    {
        TimeSpan delay = DebouncePolicy.ComputeFlushDelay(TimeSpan.FromMilliseconds(1900), Min, Max);

        delay.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    /// <summary>At or past the cap the burst flushes immediately.</summary>
    [Test]
    public void AtOrPastTheCapFlushesImmediately()
    {
        DebouncePolicy.ComputeFlushDelay(Max, Min, Max).Should().Be(TimeSpan.Zero);
        DebouncePolicy.ComputeFlushDelay(TimeSpan.FromSeconds(3), Min, Max).Should().Be(TimeSpan.Zero);
    }

    /// <summary>A zero quiet window disables debouncing: every event flushes immediately.</summary>
    [Test]
    public void ZeroQuietWindowFlushesEveryEvent()
    {
        TimeSpan delay = DebouncePolicy.ComputeFlushDelay(TimeSpan.Zero, TimeSpan.Zero, Max);

        delay.Should().Be(TimeSpan.Zero);
    }
}
