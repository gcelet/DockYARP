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
    /// <param name="networkAddresses">Map of network name to the container's IP on that network (may be empty).</param>
    /// <param name="preferredNetwork">Network to prefer when the container is attached to it, or <see langword="null"/>.</param>
    /// <param name="proxyNetworks">Networks the proxy is attached to; when non-empty, the fallback is restricted to shared (reachable) networks.</param>
    /// <returns>
    /// The preferred network's IP when available; otherwise the first usable IP by ordinal network name,
    /// skipping <c>ingress</c> and (when <paramref name="proxyNetworks"/> is non-empty) networks the proxy does
    /// not share; or <see langword="null"/> when no usable address exists.
    /// </returns>
    public static string? Select(
        IReadOnlyDictionary<string, string?> networkAddresses,
        string? preferredNetwork,
        IReadOnlyCollection<string> proxyNetworks)
    {
        ArgumentNullException.ThrowIfNull(networkAddresses);
        ArgumentNullException.ThrowIfNull(proxyNetworks);

        if (preferredNetwork is { Length: > 0 }
            && networkAddresses.TryGetValue(preferredNetwork, out string? preferred)
            && !string.IsNullOrEmpty(preferred))
        {
            return preferred;
        }

        IEnumerable<KeyValuePair<string, string?>> usable = networkAddresses
            .Where(pair => !string.IsNullOrEmpty(pair.Value)
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
            .Select(pair => pair.Value)
            .FirstOrDefault();
    }
}
