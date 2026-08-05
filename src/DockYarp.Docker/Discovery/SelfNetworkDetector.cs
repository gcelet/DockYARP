namespace DockYarp.Docker.Discovery;

using System;
using System.Collections.Generic;

/// <summary>Pure helpers for resolving the proxy's own container and the reachable network set.</summary>
/// <remarks>The live self-inspection lives in the container source; this keeps the decisions unit-testable.</remarks>
public static class SelfNetworkDetector
{
    /// <summary>Resolves the proxy's own container id from its hostname.</summary>
    /// <param name="hostname">The <c>HOSTNAME</c> value (the Docker daemon sets it to the container id by default).</param>
    /// <returns>The trimmed hostname, or <see langword="null"/> when it is null or whitespace.</returns>
    public static string? ResolveOwnContainerId(string? hostname) =>
        string.IsNullOrWhiteSpace(hostname) ? null : hostname.Trim();

    /// <summary>Chooses the reachable network set: the configured one when non-empty, otherwise the detected one.</summary>
    /// <param name="configured">The operator-configured <c>Docker:ProxyNetworks</c>.</param>
    /// <param name="detected">The proxy's own detected networks (empty when self-detection failed).</param>
    /// <returns>The set to use for reachability-aware selection.</returns>
    public static IReadOnlyCollection<string> ChooseReachableNetworks(
        IReadOnlyCollection<string> configured, IReadOnlyCollection<string> detected)
    {
        ArgumentNullException.ThrowIfNull(configured);
        ArgumentNullException.ThrowIfNull(detected);
        return configured.Count > 0 ? configured : detected;
    }
}
