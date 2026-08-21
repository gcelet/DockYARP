namespace DockYarp.IntegrationTests;

using System.Collections.Generic;
using System.Linq;
using System.Net;

using AwesomeAssertions;

using DockYarp.App.ReverseProxy;

using Microsoft.AspNetCore.Http;

using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.SessionAffinity;

/// <summary>Tests for <see cref="ClientIpHashSessionAffinityPolicy"/>.</summary>
public sealed class ClientIpHashSessionAffinityPolicyTests
{
    private static readonly ClusterState Cluster = new("app");
    private static readonly SessionAffinityConfig Config = new();
    private static readonly ClientIpHashSessionAffinityPolicy Policy = new();

    /// <summary>Repeated calls from the same client IP consistently select the same destination.</summary>
    [Test]
    public void SameClientIpSelectsSameDestination()
    {
        DestinationState[] destinations = [new("d1"), new("d2"), new("d3")];

        AffinityResult first = Find("203.0.113.10", destinations);
        AffinityResult second = Find("203.0.113.10", destinations);

        first.Status.Should().Be(AffinityStatus.OK);
        first.Destinations.Should().HaveCount(1);
        second.Destinations!.Single().DestinationId.Should().Be(first.Destinations![0].DestinationId);
    }

    /// <summary>Clients sharing the first 3 octets of an IPv4 address (nginx's own ip_hash grouping) select the
    /// same destination, even though the last octet differs.</summary>
    [Test]
    public void SameSubnetSelectsSameDestination()
    {
        DestinationState[] destinations = [new("d1"), new("d2"), new("d3")];

        AffinityResult a = Find("203.0.113.10", destinations);
        AffinityResult b = Find("203.0.113.250", destinations);

        b.Destinations!.Single().DestinationId.Should().Be(a.Destinations![0].DestinationId);
    }

    /// <summary>Different IPv4 /24 subnets can select different destinations (the hash actually varies).</summary>
    [Test]
    public void DifferentSubnetsCanSelectDifferentDestinations()
    {
        DestinationState[] destinations = [new("d1"), new("d2"), new("d3"), new("d4"), new("d5")];

        HashSet<string> selected =
        [
            Find("10.0.0.1", destinations).Destinations!.Single().DestinationId,
            Find("10.0.1.1", destinations).Destinations!.Single().DestinationId,
            Find("172.16.5.1", destinations).Destinations!.Single().DestinationId,
            Find("198.51.100.1", destinations).Destinations!.Single().DestinationId,
        ];

        selected.Should().HaveCountGreaterThan(1, "different /24 subnets should not all collide onto one destination");
    }

    /// <summary>IPv6 addresses are hashed on their full address (not truncated the way IPv4 is).</summary>
    [Test]
    public void Ipv6HashesOnFullAddress()
    {
        DestinationState[] destinations = [new("d1"), new("d2"), new("d3")];

        AffinityResult a = Find("2001:db8::1", destinations);
        AffinityResult b = Find("2001:db8::2", destinations);

        a.Status.Should().Be(AffinityStatus.OK);
        b.Status.Should().Be(AffinityStatus.OK);
    }

    /// <summary>A destination-list change (e.g. a container removed) redistributes rather than throwing —
    /// the policy has no memory of a "previous" selection to invalidate.</summary>
    [Test]
    public void DestinationListChangeRedistributesWithoutThrowing()
    {
        AffinityResult before = Find("203.0.113.10", [new("d1"), new("d2"), new("d3")]);
        AffinityResult after = Find("203.0.113.10", [new("d2"), new("d3")]);

        before.Status.Should().Be(AffinityStatus.OK);
        after.Status.Should().Be(AffinityStatus.OK);
        after.Destinations!.Single().DestinationId.Should().BeOneOf("d2", "d3");
    }

    /// <summary>No remote IP (defensive; should not happen for a real HTTP connection) yields
    /// AffinityKeyNotSet, not a thrown exception.</summary>
    [Test]
    public void NoRemoteIpYieldsAffinityKeyNotSet()
    {
        DefaultHttpContext context = new();

        AffinityResult result = Policy.FindAffinitizedDestinations(context, Cluster, Config, [new("d1")]);

        result.Status.Should().Be(AffinityStatus.AffinityKeyNotSet);
    }

    /// <summary>An empty destination list yields AffinityKeyNotSet, not a divide-by-zero.</summary>
    [Test]
    public void NoDestinationsYieldsAffinityKeyNotSet()
    {
        AffinityResult result = Find("203.0.113.10", []);

        result.Status.Should().Be(AffinityStatus.AffinityKeyNotSet);
    }

    /// <summary>AffinitizeResponse is a documented no-op — it must not throw and must not mutate the response.</summary>
    [Test]
    public void AffinitizeResponseIsANoOp()
    {
        DefaultHttpContext context = new();

        Policy.AffinitizeResponse(context, Cluster, Config, new DestinationState("d1"));

        context.Response.Headers.Should().BeEmpty();
    }

    private static AffinityResult Find(string clientIp, IReadOnlyList<DestinationState> destinations)
    {
        DefaultHttpContext context = new() { Connection = { RemoteIpAddress = IPAddress.Parse(clientIp) } };
        return Policy.FindAffinitizedDestinations(context, Cluster, Config, destinations);
    }
}
