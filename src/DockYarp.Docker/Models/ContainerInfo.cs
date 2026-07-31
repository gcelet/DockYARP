namespace DockYarp.Docker.Models;

using System.Collections.Generic;
using System.Collections.Immutable;

/// <summary>A normalized view of a Docker container relevant to discovery.</summary>
public sealed record ContainerInfo
{
    /// <summary>Gets the container id (stable endpoint identity).</summary>
    public required string Id { get; init; }

    /// <summary>Gets the container name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the reachable network address (IP or resolvable host) DockYarp forwards to.</summary>
    public required string Address { get; init; }

    /// <summary>Gets a value indicating whether the container runs in Docker host network mode.</summary>
    public bool IsHostNetwork { get; init; }

    /// <summary>Gets the container labels.</summary>
    public required IReadOnlyDictionary<string, string> Labels { get; init; }

    /// <summary>Gets the container environment variables (nginx-proxy's canonical config channel).</summary>
    public IReadOnlyDictionary<string, string> Env { get; init; } =
        new Dictionary<string, string>(System.StringComparer.Ordinal);

    /// <summary>Gets the container's exposed (private) ports.</summary>
    public ImmutableArray<int> ExposedPorts { get; init; } = [];

    /// <summary>Gets the container's Docker health state; <see cref="ContainerHealth.None"/> when no check is declared.</summary>
    public ContainerHealth Health { get; init; } = ContainerHealth.None;
}
