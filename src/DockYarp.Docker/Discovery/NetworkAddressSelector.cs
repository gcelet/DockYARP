namespace DockYarp.Docker.Discovery;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Selects the container IP to forward to among its Docker networks.</summary>
/// <remarks>Pure and side-effect free so it can be unit tested without a Docker daemon.</remarks>
public static class NetworkAddressSelector
{
    // Swarm's routing-mesh network; its IP is not routable for direct proxying, so it is never auto-selected.
    private const string IngressNetwork = "ingress";

    /// <summary>Chooses the address from the container's networks.</summary>
    /// <param name="networkAddresses">Map of network name to the container's addresses (both families) on that network (may be empty).</param>
    /// <param name="preferredNetwork">Network to prefer when the container is attached to it, or <see langword="null"/>.</param>
    /// <param name="proxyNetworks">Networks the proxy is attached to; when non-empty, the fallback is restricted to shared (reachable) networks.</param>
    /// <param name="preference">Which address family to prefer within the chosen network.</param>
    /// <returns>
    /// The preferred network's IP when available; otherwise the first usable IP by ordinal network name,
    /// skipping <c>ingress</c> and (when <paramref name="proxyNetworks"/> is non-empty) networks the proxy does
    /// not share; or <see langword="null"/> when no usable address exists. Within the chosen network the
    /// <paramref name="preference"/> family is returned, falling back to the other family when it is absent.
    /// </returns>
    public static string? Select(
        IReadOnlyDictionary<string, NetworkAddresses> networkAddresses,
        string? preferredNetwork,
        IReadOnlyCollection<string> proxyNetworks,
        AddressFamilyPreference preference)
    {
        ArgumentNullException.ThrowIfNull(networkAddresses);
        ArgumentNullException.ThrowIfNull(proxyNetworks);

        if (preferredNetwork is { Length: > 0 }
            && networkAddresses.TryGetValue(preferredNetwork, out NetworkAddresses preferred)
            && Pick(preferred, preference) is { } preferredIp)
        {
            return preferredIp;
        }

        IEnumerable<KeyValuePair<string, NetworkAddresses>> usable = networkAddresses
            .Where(pair => Pick(pair.Value, preference) is not null
                && !string.Equals(pair.Key, IngressNetwork, StringComparison.OrdinalIgnoreCase));

        // With the proxy's own networks known, only a shared network is reachable; a backend sharing none is
        // left without an address (skipped later) rather than pointed at an unreachable IP.
        if (proxyNetworks.Count > 0)
        {
            HashSet<string> reachable = new(proxyNetworks, StringComparer.Ordinal);
            usable = usable.Where(pair => reachable.Contains(pair.Key));
        }

        return usable
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => Pick(pair.Value, preference))
            .FirstOrDefault();
    }

    // Return the preferred family's address, falling back to the other family; null when the network has neither.
    private static string? Pick(NetworkAddresses address, AddressFamilyPreference preference) =>
        preference == AddressFamilyPreference.Ipv6
            ? First(address.Ipv6, address.Ipv4)
            : First(address.Ipv4, address.Ipv6);

    private static string? First(string? preferred, string? other)
    {
        if (preferred is { Length: > 0 })
        {
            return preferred;
        }

        return other is { Length: > 0 } ? other : null;
    }
}
