namespace DockYarp.Docker.Tests;

using System;
using System.Collections.Generic;

using AwesomeAssertions;

using DockYarp.Docker.Discovery;

/// <summary>Tests for <see cref="BackendAddressResolver"/>.</summary>
public sealed class BackendAddressResolverTests
{
    private static Dictionary<string, NetworkAddresses> Networks(params (string Name, string? Ip)[] entries)
    {
        Dictionary<string, NetworkAddresses> networks = new(StringComparer.Ordinal);
        foreach ((string Name, string? Ip) entry in entries)
        {
            networks[entry.Name] = new NetworkAddresses(entry.Ip, null);
        }

        return networks;
    }

    /// <summary>A container attached to the reserved host network is detected as host mode.</summary>
    [Test]
    public void HostNetworkIsDetected()
    {
        BackendAddressResolver.IsHostNetwork(Networks(("host", null))).Should().BeTrue();
        BackendAddressResolver.IsHostNetwork(Networks(("bridge", "10.0.0.1"))).Should().BeFalse();
        BackendAddressResolver.IsHostNetwork(Networks()).Should().BeFalse();
    }

    /// <summary>A host-mode container targets the configured host address.</summary>
    [Test]
    public void HostModeTargetsHostAddress()
    {
        BackendAddressResolver.Resolve(
            Networks(("host", null)), hostAddress: "host.docker.internal", selectedNetworkIp: null,
            nameFallback: "web", proxyNetworks: [])
            .Should().Be("host.docker.internal");
    }

    /// <summary>A host-mode container without a configured host address yields an empty address (skip).</summary>
    [Test]
    public void HostModeWithoutHostAddressIsEmpty()
    {
        BackendAddressResolver.Resolve(
            Networks(("host", null)), hostAddress: null, selectedNetworkIp: null,
            nameFallback: "web", proxyNetworks: [])
            .Should().BeEmpty();
    }

    /// <summary>A networked container uses its selected IP.</summary>
    [Test]
    public void NetworkedContainerUsesSelectedIp()
    {
        BackendAddressResolver.Resolve(
            Networks(("bridge", "10.0.0.7")), hostAddress: null, selectedNetworkIp: "10.0.0.7",
            nameFallback: "web", proxyNetworks: [])
            .Should().Be("10.0.0.7");
    }

    /// <summary>With reachability known and no selected IP, the backend is skipped (empty).</summary>
    [Test]
    public void KnownReachabilityWithoutIpIsEmpty()
    {
        BackendAddressResolver.Resolve(
            Networks(("remote", "10.0.0.7")), hostAddress: null, selectedNetworkIp: null,
            nameFallback: "web", proxyNetworks: ["edge"])
            .Should().BeEmpty();
    }

    /// <summary>With reachability unknown and no selected IP, the container name is used.</summary>
    [Test]
    public void UnknownReachabilityWithoutIpUsesName()
    {
        BackendAddressResolver.Resolve(
            Networks(("bridge", null)), hostAddress: null, selectedNetworkIp: null,
            nameFallback: "web", proxyNetworks: [])
            .Should().Be("web");
    }
}
