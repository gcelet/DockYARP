namespace DockYarp.Docker.Tests;

using System.Collections.Generic;

using AwesomeAssertions;

using DockYarp.Docker.Discovery;

/// <summary>Tests for <see cref="SelfNetworkDetector"/> (own-id resolution + reachable-set choice).</summary>
public sealed class SelfNetworkDetectorTests
{
    /// <summary>The own container id is the trimmed hostname, or null when it is empty/whitespace.</summary>
    [Test]
    public void ResolveOwnContainerIdTrimsAndNullsEmpty()
    {
        SelfNetworkDetector.ResolveOwnContainerId("abc123").Should().Be("abc123");
        SelfNetworkDetector.ResolveOwnContainerId("  abc123  ").Should().Be("abc123");
        SelfNetworkDetector.ResolveOwnContainerId(string.Empty).Should().BeNull();
        SelfNetworkDetector.ResolveOwnContainerId("   ").Should().BeNull();
        SelfNetworkDetector.ResolveOwnContainerId(null).Should().BeNull();
    }

    /// <summary>A configured reachable set wins over the detected one.</summary>
    [Test]
    public void ChooseReachableNetworksPrefersConfigured()
    {
        IReadOnlyCollection<string> configured = ["edge"];
        IReadOnlyCollection<string> detected = ["dcp"];

        SelfNetworkDetector.ChooseReachableNetworks(configured, detected).Should().Equal("edge");
    }

    /// <summary>With no configured set, the detected networks are used.</summary>
    [Test]
    public void ChooseReachableNetworksFallsBackToDetected() =>
        SelfNetworkDetector.ChooseReachableNetworks([], ["dcp", "edge"]).Should().Equal("dcp", "edge");

    /// <summary>With neither configured nor detected, the reachable set is empty (reachability-unaware).</summary>
    [Test]
    public void ChooseReachableNetworksEmptyWhenBothEmpty() =>
        SelfNetworkDetector.ChooseReachableNetworks([], []).Should().BeEmpty();
}
