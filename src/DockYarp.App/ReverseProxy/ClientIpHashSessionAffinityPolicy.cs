namespace DockYarp.App.ReverseProxy;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using Microsoft.AspNetCore.Http;

using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.SessionAffinity;

/// <summary>Client-IP-hash session affinity: nginx-proxy <c>ip_hash</c> parity.</summary>
/// <remarks>
/// Deterministic function of (client IP, healthy destination list) → destination — stateless, no cookie or
/// header, unlike YARP's built-in policies. Has an effect from the client's very first request, and needs no
/// Data Protection (there is no key to encrypt: the client's own IP is always available on every request).
/// </remarks>
public sealed class ClientIpHashSessionAffinityPolicy : ISessionAffinityPolicy
{
    /// <summary>The policy name referenced from <see cref="SessionAffinityConfig.Policy"/>.</summary>
    public const string PolicyName = "ClientIpHash";

    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;

    /// <inheritdoc />
    public string Name => PolicyName;

    /// <inheritdoc />
    public AffinityResult FindAffinitizedDestinations(
        HttpContext context,
        ClusterState cluster,
        SessionAffinityConfig config,
        IReadOnlyList<DestinationState> destinations)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(destinations);
        IPAddress? remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp is null || destinations.Count == 0)
        {
            return new AffinityResult(null, AffinityStatus.AffinityKeyNotSet);
        }

        // Sort by DestinationId for a stable order across calls — the live destination list's own enumeration
        // order is not guaranteed stable, and the hash-to-index mapping must be deterministic.
        DestinationState[] ordered = [.. destinations.OrderBy(destination => destination.DestinationId, StringComparer.Ordinal)];
        uint hash = HashClientIp(remoteIp);
        DestinationState selected = ordered[(int)(hash % (uint)ordered.Length)];
        return new AffinityResult([selected], AffinityStatus.OK);
    }

    /// <inheritdoc />
    /// <remarks>No-op: this policy stores nothing on the response — the client's own IP is always available on
    /// every subsequent request, unlike a cookie/header policy which must attach a key to be echoed back.</remarks>
    public void AffinitizeResponse(
        HttpContext context, ClusterState cluster, SessionAffinityConfig config, DestinationState destination)
    {
    }

    // Matches nginx's own ip_hash algorithm: hash only the first 3 octets of an IPv4 address (so clients from
    // the same dynamic-IP /24 subnet stay on one destination); hash the full address for IPv6.
    private static uint HashClientIp(IPAddress address)
    {
        Span<byte> buffer = stackalloc byte[16];
        if (!address.TryWriteBytes(buffer, out int written))
        {
            return FnvOffsetBasis;
        }

        int length = written == 4 ? 3 : written;
        return Fnv1a(buffer[..length]);
    }

    // FNV-1a: a small, well-defined, non-cryptographic hash — deterministic across processes and restarts,
    // unlike relying on a type's own GetHashCode() (not guaranteed stable for this purpose).
    private static uint Fnv1a(ReadOnlySpan<byte> data)
    {
        uint hash = FnvOffsetBasis;
        foreach (byte value in data)
        {
            hash ^= value;
            hash *= FnvPrime;
        }

        return hash;
    }
}
