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
    /// <returns>
    /// The preferred network's IP when available; otherwise the first usable IP by ordinal network name,
    /// skipping <c>ingress</c>; or <see langword="null"/> when no usable address exists.
    /// </returns>
    public static string? Select(IReadOnlyDictionary<string, string?> networkAddresses, string? preferredNetwork)
    {
        ArgumentNullException.ThrowIfNull(networkAddresses);

        if (preferredNetwork is { Length: > 0 }
            && networkAddresses.TryGetValue(preferredNetwork, out string? preferred)
            && !string.IsNullOrEmpty(preferred))
        {
            return preferred;
        }

        return networkAddresses
            .Where(pair => !string.IsNullOrEmpty(pair.Value)
                && !string.Equals(pair.Key, IngressNetwork, StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value)
            .FirstOrDefault();
    }
}
