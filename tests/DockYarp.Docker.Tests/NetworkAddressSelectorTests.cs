namespace DockYarp.Docker.Tests;

using System;
using System.Collections.Generic;

using AwesomeAssertions;

using DockYarp.Docker.Discovery;

/// <summary>Tests for <see cref="NetworkAddressSelector"/>.</summary>
public sealed class NetworkAddressSelectorTests
{
    /// <summary>The preferred network's IP is used when the container is attached to it.</summary>
    [Test]
    public void PreferredNetworkIsUsed()
    {
        Dictionary<string, string?> networks = new(StringComparer.Ordinal)
        {
            ["frontend"] = "10.0.1.2",
            ["backend"] = "10.0.2.2",
        };

        NetworkAddressSelector.Select(networks, preferredNetwork: "backend").Should().Be("10.0.2.2");
    }

    /// <summary>The Swarm ingress network is skipped when no preferred network is set.</summary>
    [Test]
    public void IngressIsSkipped()
    {
        Dictionary<string, string?> networks = new(StringComparer.Ordinal)
        {
            ["ingress"] = "10.0.0.5",
            ["app"] = "10.0.1.5",
        };

        NetworkAddressSelector.Select(networks, preferredNetwork: null).Should().Be("10.0.1.5");
    }

    /// <summary>Without a preferred network, selection is deterministic (ordinal by network name).</summary>
    [Test]
    public void SelectionIsDeterministic()
    {
        Dictionary<string, string?> networks = new(StringComparer.Ordinal)
        {
            ["b-net"] = "10.0.2.2",
            ["a-net"] = "10.0.1.1",
        };

        NetworkAddressSelector.Select(networks, preferredNetwork: null).Should().Be("10.0.1.1");
    }

    /// <summary>No usable network address yields null (the caller then falls back to the container name).</summary>
    [Test]
    public void NoUsableNetworkYieldsNull()
    {
        NetworkAddressSelector.Select(new Dictionary<string, string?>(StringComparer.Ordinal), preferredNetwork: null)
            .Should().BeNull();
        NetworkAddressSelector.Select(
            new Dictionary<string, string?>(StringComparer.Ordinal) { ["ingress"] = "10.0.0.5" }, null)
            .Should().BeNull();
    }
}
