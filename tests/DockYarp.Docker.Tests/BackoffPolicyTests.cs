namespace DockYarp.Docker.Tests;

using System;

using AwesomeAssertions;

using DockYarp.Docker.Discovery;

/// <summary>Tests for <see cref="BackoffPolicy"/>.</summary>
public sealed class BackoffPolicyTests
{
    /// <summary>The first attempt waits the initial delay.</summary>
    [Test]
    public void FirstAttemptUsesInitialDelay()
    {
        TimeSpan delay = BackoffPolicy.Compute(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));

        delay.Should().Be(TimeSpan.FromSeconds(1));
    }

    /// <summary>The delay grows exponentially but is capped at the maximum.</summary>
    [Test]
    public void DelayIsCappedAtMaximum()
    {
        TimeSpan delay = BackoffPolicy.Compute(100, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));

        delay.Should().Be(TimeSpan.FromSeconds(30));
    }

    /// <summary>The delay doubles between consecutive attempts (within the cap).</summary>
    [Test]
    public void DelayDoublesPerAttempt()
    {
        TimeSpan third = BackoffPolicy.Compute(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));

        third.Should().Be(TimeSpan.FromSeconds(4));
    }
}
