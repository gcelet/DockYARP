namespace DockYarp.Docker.Tests;

using System;
using System.Collections.Generic;

using AwesomeAssertions;

using DockYarp.Docker.Discovery;

/// <summary>Tests for <see cref="NetworkAddressSelector"/>.</summary>
public sealed class NetworkAddressSelectorTests
{
    private const AddressFamilyPreference Ipv4 = AddressFamilyPreference.Ipv4;
    private const AddressFamilyPreference Ipv6 = AddressFamilyPreference.Ipv6;

    /// <summary>The preferred network's IP is used when the container is attached to it.</summary>
    [Test]
    public void PreferredNetworkIsUsed()
    {
        Dictionary<string, NetworkAddresses> networks = V4(("frontend", "10.0.1.2"), ("backend", "10.0.2.2"));

        NetworkAddressSelector.Select(networks, "backend", [], Ipv4).Should().Be("10.0.2.2");
    }

    /// <summary>The Swarm ingress network is skipped when no preferred network is set.</summary>
    [Test]
    public void IngressIsSkipped()
    {
        Dictionary<string, NetworkAddresses> networks = V4(("ingress", "10.0.0.5"), ("app", "10.0.1.5"));

        NetworkAddressSelector.Select(networks, null, [], Ipv4).Should().Be("10.0.1.5");
    }

    /// <summary>Without a preferred network, selection is deterministic (ordinal by network name).</summary>
    [Test]
    public void SelectionIsDeterministic()
    {
        Dictionary<string, NetworkAddresses> networks = V4(("b-net", "10.0.2.2"), ("a-net", "10.0.1.1"));

        NetworkAddressSelector.Select(networks, null, [], Ipv4).Should().Be("10.0.1.1");
    }

    /// <summary>No usable network address yields null (the caller then falls back to the container name).</summary>
    [Test]
    public void NoUsableNetworkYieldsNull()
    {
        NetworkAddressSelector.Select(
            new Dictionary<string, NetworkAddresses>(StringComparer.Ordinal), null, [], Ipv4)
            .Should().BeNull();
        NetworkAddressSelector.Select(V4(("ingress", "10.0.0.5")), null, [], Ipv4).Should().BeNull();
    }

    /// <summary>With proxy networks known, the shared (reachable) network's IP is selected among several.</summary>
    [Test]
    public void SharedReachableNetworkIsSelected()
    {
        Dictionary<string, NetworkAddresses> networks = V4(("a-remote", "10.0.1.1"), ("shared", "10.0.9.9"));

        NetworkAddressSelector.Select(networks, null, ["shared"], Ipv4).Should().Be("10.0.9.9");
    }

    /// <summary>A backend on no network the proxy shares yields null (skipped, not routed).</summary>
    [Test]
    public void UnreachableBackendYieldsNull()
    {
        Dictionary<string, NetworkAddresses> networks = V4(("a-remote", "10.0.1.1"), ("b-remote", "10.0.2.2"));

        NetworkAddressSelector.Select(networks, null, ["shared"], Ipv4).Should().BeNull();
    }

    /// <summary>An explicit preferred network still wins even when proxy networks are configured.</summary>
    [Test]
    public void PreferredNetworkWinsOverReachabilityFilter()
    {
        Dictionary<string, NetworkAddresses> networks = V4(("backend", "10.0.2.2"), ("shared", "10.0.9.9"));

        NetworkAddressSelector.Select(networks, "backend", ["shared"], Ipv4).Should().Be("10.0.2.2");
    }

    /// <summary>With both families present and no IPv6 preference, the IPv4 address is used.</summary>
    [Test]
    public void Ipv4IsUsedByDefault()
    {
        Dictionary<string, NetworkAddresses> networks = new(StringComparer.Ordinal)
        {
            ["app"] = new NetworkAddresses("10.0.1.5", "fd00::5"),
        };

        NetworkAddressSelector.Select(networks, null, [], Ipv4).Should().Be("10.0.1.5");
    }

    /// <summary>With both families present and IPv6 preferred, the IPv6 address is used (single family).</summary>
    [Test]
    public void Ipv6IsPreferredWhenEnabled()
    {
        Dictionary<string, NetworkAddresses> networks = new(StringComparer.Ordinal)
        {
            ["app"] = new NetworkAddresses("10.0.1.5", "fd00::5"),
        };

        NetworkAddressSelector.Select(networks, null, [], Ipv6).Should().Be("fd00::5");
    }

    /// <summary>The preferred family falls back to the other when absent (IPv6 preferred but only IPv4 present,
    /// and — symmetrically — an IPv6-only network is routable under the default IPv4 preference).</summary>
    [Test]
    public void FallsBackToTheOtherFamily()
    {
        Dictionary<string, NetworkAddresses> ipv4Only = new(StringComparer.Ordinal)
        {
            ["app"] = new NetworkAddresses("10.0.1.5", null),
        };
        Dictionary<string, NetworkAddresses> ipv6Only = new(StringComparer.Ordinal)
        {
            ["app"] = new NetworkAddresses(null, "fd00::5"),
        };

        NetworkAddressSelector.Select(ipv4Only, null, [], Ipv6).Should().Be("10.0.1.5");
        NetworkAddressSelector.Select(ipv6Only, null, [], Ipv4).Should().Be("fd00::5");
    }

    private static Dictionary<string, NetworkAddresses> V4(params (string Name, string Ip)[] entries)
    {
        Dictionary<string, NetworkAddresses> map = new(StringComparer.Ordinal);
        foreach ((string name, string ip) in entries)
        {
            map[name] = new NetworkAddresses(ip, null);
        }

        return map;
    }
}
