namespace DockYarp.Docker.Discovery;

using System;

/// <summary>Options controlling Docker discovery.</summary>
public sealed class DockerDiscoveryOptions
{
    /// <summary>Gets or sets the Docker endpoint URI; <see langword="null"/> uses the platform default.</summary>
    /// <remarks>For example <c>unix:///var/run/docker.sock</c> or <c>npipe://./pipe/docker_engine</c>.</remarks>
    public string? DockerEndpoint { get; set; }

    /// <summary>Gets or sets the Docker network whose IP is preferred when a container is attached to it.</summary>
    /// <remarks>Set this to the network the proxy shares with its backends; <see langword="null"/> selects deterministically.</remarks>
    public string? PreferredNetwork { get; set; }

    /// <summary>Gets or sets the initial reconnect delay after a connection failure.</summary>
    public TimeSpan InitialReconnectDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets or sets the maximum reconnect delay (backoff ceiling).</summary>
    public TimeSpan MaxReconnectDelay { get; set; } = TimeSpan.FromSeconds(30);
}
