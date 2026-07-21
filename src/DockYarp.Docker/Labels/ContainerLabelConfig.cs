namespace DockYarp.Docker.Labels;

using DockYarp.Core.Models;

/// <summary>Strongly-typed configuration parsed from a container's labels.</summary>
public sealed record ContainerLabelConfig
{
    /// <summary>Gets the host the container is exposed on (<c>VIRTUAL_HOST</c>).</summary>
    public required string Host { get; init; }

    /// <summary>Gets the target container port (<c>VIRTUAL_PORT</c>, or inferred).</summary>
    public required int Port { get; init; }

    /// <summary>Gets the optional path prefix (<c>VIRTUAL_PATH</c>).</summary>
    public string? PathPrefix { get; init; }

    /// <summary>Gets the certificate host (<c>LETSENCRYPT_HOST</c>), if any.</summary>
    public string? LetsEncryptHost { get; init; }

    /// <summary>Gets the certificate contact email (<c>LETSENCRYPT_EMAIL</c>), if any.</summary>
    public string? LetsEncryptEmail { get; init; }

    /// <summary>Gets the requested load-balancing policy (<c>DOCKYARP_LB</c>), if any.</summary>
    public LoadBalancingPolicy? LoadBalancingPolicy { get; init; }
}
