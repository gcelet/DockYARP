namespace DockYarp.Docker.Discovery;

using System;
using System.Collections.Generic;

/// <summary>Options controlling Docker discovery.</summary>
public sealed class DockerDiscoveryOptions
{
    /// <summary>Gets or sets the Docker endpoint URI; <see langword="null"/> uses the platform default.</summary>
    /// <remarks>For example <c>unix:///var/run/docker.sock</c> or <c>npipe://./pipe/docker_engine</c>.</remarks>
    public string? DockerEndpoint { get; set; }

    /// <summary>Gets the Docker-native inclusion filters scoping which containers are discovered.</summary>
    /// <remarks>
    /// Maps a Docker filter key (<c>label</c>, <c>name</c>, <c>network</c>, …) to its accepted values; values
    /// within a key are OR-combined and distinct keys are AND-combined. Empty selects all containers. Applied
    /// to the authoritative container listing, e.g. <c>ContainerFilters:label:0 = dockyarp.enable=true</c>.
    /// </remarks>
    public IDictionary<string, IList<string>> ContainerFilters { get; } =
        new Dictionary<string, IList<string>>(StringComparer.Ordinal);

    /// <summary>Gets or sets the Docker network whose IP is preferred when a container is attached to it.</summary>
    /// <remarks>Set this to the network the proxy shares with its backends; <see langword="null"/> selects deterministically.</remarks>
    public string? PreferredNetwork { get; set; }

    /// <summary>Gets or sets the address the proxy uses to reach the Docker host, for host-network backends.</summary>
    /// <remarks>
    /// A host-network container has no container IP; DockYarp forwards to this address (e.g.
    /// <c>host.docker.internal</c> on Docker Desktop, or the host-gateway/LAN IP on Linux). When unset, a
    /// host-network backend is skipped rather than routed to a guessed address.
    /// </remarks>
    public string? HostAddress { get; set; }

    /// <summary>Gets the Docker networks the proxy is attached to, used to select a reachable backend address.</summary>
    /// <remarks>
    /// When non-empty, address selection restricts the fallback choice to a shared (reachable) network and
    /// skips backends reachable on none. Empty leaves selection reachability-unaware (prior behavior), e.g.
    /// <c>ProxyNetworks:0 = edge</c>.
    /// </remarks>
    public IList<string> ProxyNetworks { get; } = [];

    /// <summary>Gets or sets the initial reconnect delay after a connection failure.</summary>
    public TimeSpan InitialReconnectDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets or sets the maximum reconnect delay (backoff ceiling).</summary>
    public TimeSpan MaxReconnectDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the quiet window after a Docker event before an event-driven reconcile runs.</summary>
    /// <remarks>
    /// A burst of events within this window coalesces into a single reconciliation; each further event extends
    /// the window (up to <see cref="ReconcileDebounceMax"/>). Startup and post-reconnect reconciliations are
    /// immediate and unaffected. Zero disables debouncing (one reconcile per event).
    /// </remarks>
    public TimeSpan ReconcileDebounceMin { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Gets or sets the maximum time an event-driven reconcile is deferred from a burst's first event.</summary>
    /// <remarks>Bounds worst-case latency for a burst that never settles within <see cref="ReconcileDebounceMin"/>.</remarks>
    public TimeSpan ReconcileDebounceMax { get; set; } = TimeSpan.FromSeconds(2);
}
