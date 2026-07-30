namespace DockYarp.Docker.Discovery;

using System;
using System.Collections.Generic;

/// <summary>Decides the backend host address (not the port) DockYarp forwards a container's traffic to.</summary>
/// <remarks>Pure and side-effect free so it can be unit tested without a Docker daemon.</remarks>
public static class BackendAddressResolver
{
    // Reserved Docker network name for host network mode; users cannot create a network named "host", so its
    // presence is a reliable host-mode signal available from the container list API.
    private const string HostNetwork = "host";

    /// <summary>Determines whether the container runs in host network mode.</summary>
    /// <param name="networkAddresses">Map of network name to the container's IP on that network.</param>
    /// <returns><see langword="true"/> when the container is attached to the reserved <c>host</c> network.</returns>
    public static bool IsHostNetwork(IReadOnlyDictionary<string, string?> networkAddresses)
    {
        ArgumentNullException.ThrowIfNull(networkAddresses);
        return networkAddresses.ContainsKey(HostNetwork);
    }

    /// <summary>Resolves the backend host address.</summary>
    /// <param name="networkAddresses">Map of network name to the container's IP on that network.</param>
    /// <param name="hostAddress">The configured Docker host address, or <see langword="null"/>.</param>
    /// <param name="selectedNetworkIp">The IP chosen among the container's networks, or <see langword="null"/>.</param>
    /// <param name="nameFallback">The container name, usable when the proxy's networks are unknown.</param>
    /// <param name="proxyNetworks">The proxy's own networks; a non-empty set means reachability is known.</param>
    /// <returns>The backend host address, or an empty string when the backend should be skipped.</returns>
    public static string Resolve(
        IReadOnlyDictionary<string, string?> networkAddresses,
        string? hostAddress,
        string? selectedNetworkIp,
        string nameFallback,
        IReadOnlyCollection<string> proxyNetworks)
    {
        ArgumentNullException.ThrowIfNull(nameFallback);
        ArgumentNullException.ThrowIfNull(proxyNetworks);

        if (IsHostNetwork(networkAddresses))
        {
            // Host mode exposes no container IP; target the configured Docker host, else leave empty to skip.
            return hostAddress is { Length: > 0 } ? hostAddress : string.Empty;
        }

        if (!string.IsNullOrEmpty(selectedNetworkIp))
        {
            return selectedNetworkIp;
        }

        // Proxy networks unknown: fall back to the container name (resolvable on a shared network). Known but no
        // shared network: leave empty so the caller skips the unreachable backend.
        return proxyNetworks.Count > 0 ? string.Empty : nameFallback;
    }
}
