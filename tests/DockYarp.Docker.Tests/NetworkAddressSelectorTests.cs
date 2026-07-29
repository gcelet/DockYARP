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

        NetworkAddressSelector.Select(networks, preferredNetwork: "backend", proxyNetworks: []).Should().Be("10.0.2.2");
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

        NetworkAddressSelector.Select(networks, preferredNetwork: null, proxyNetworks: []).Should().Be("10.0.1.5");
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

        NetworkAddressSelector.Select(networks, preferredNetwork: null, proxyNetworks: []).Should().Be("10.0.1.1");
    }

    /// <summary>No usable network address yields null (the caller then falls back to the container name).</summary>
    [Test]
    public void NoUsableNetworkYieldsNull()
    {
        NetworkAddressSelector.Select(
            new Dictionary<string, string?>(StringComparer.Ordinal), preferredNetwork: null, proxyNetworks: [])
            .Should().BeNull();
        NetworkAddressSelector.Select(
            new Dictionary<string, string?>(StringComparer.Ordinal) { ["ingress"] = "10.0.0.5" }, null, [])
            .Should().BeNull();
    }

    /// <summary>With proxy networks known, the shared (reachable) network's IP is selected among several.</summary>
    [Test]
    public void SharedReachableNetworkIsSelected()
    {
        Dictionary<string, string?> networks = new(StringComparer.Ordinal)
        {
            ["a-remote"] = "10.0.1.1",
            ["shared"] = "10.0.9.9",
        };

        NetworkAddressSelector.Select(networks, preferredNetwork: null, proxyNetworks: ["shared"])
            .Should().Be("10.0.9.9");
    }

    /// <summary>A backend on no network the proxy shares yields null (skipped, not routed).</summary>
    [Test]
    public void UnreachableBackendYieldsNull()
    {
        Dictionary<string, string?> networks = new(StringComparer.Ordinal)
        {
            ["a-remote"] = "10.0.1.1",
            ["b-remote"] = "10.0.2.2",
        };

        NetworkAddressSelector.Select(networks, preferredNetwork: null, proxyNetworks: ["shared"])
            .Should().BeNull();
    }

    /// <summary>An explicit preferred network still wins even when proxy networks are configured.</summary>
    [Test]
    public void PreferredNetworkWinsOverReachabilityFilter()
    {
        Dictionary<string, string?> networks = new(StringComparer.Ordinal)
        {
            ["backend"] = "10.0.2.2",
            ["shared"] = "10.0.9.9",
        };

        NetworkAddressSelector.Select(networks, preferredNetwork: "backend", proxyNetworks: ["shared"])
            .Should().Be("10.0.2.2");
    }
}
