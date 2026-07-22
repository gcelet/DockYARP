namespace DockYarp.Docker.Labels;

using System.Collections.Immutable;

using DockYarp.Core.Models;

/// <summary>Strongly-typed configuration parsed from a container's labels.</summary>
public sealed record ContainerLabelConfig
{
    /// <summary>Gets the hosts the container is exposed on (comma-separated <c>VIRTUAL_HOST</c>).</summary>
    public required ImmutableArray<string> Hosts { get; init; }

    /// <summary>Gets the target container port (<c>VIRTUAL_PORT</c>, or inferred).</summary>
    public required int Port { get; init; }

    /// <summary>Gets the backend transport scheme (<c>VIRTUAL_PROTO</c>); defaults to HTTP.</summary>
    public BackendScheme Scheme { get; init; } = BackendScheme.Http;

    /// <summary>Gets the optional path prefix (<c>VIRTUAL_PATH</c>).</summary>
    public string? PathPrefix { get; init; }

    /// <summary>Gets the optional prefix stripped before forwarding (derived from <c>VIRTUAL_DEST</c>).</summary>
    public string? PathRemovePrefix { get; init; }

    /// <summary>Gets the certificate host (<c>LETSENCRYPT_HOST</c>), if any.</summary>
    public string? LetsEncryptHost { get; init; }

    /// <summary>Gets the certificate contact email (<c>LETSENCRYPT_EMAIL</c>), if any.</summary>
    public string? LetsEncryptEmail { get; init; }

    /// <summary>Gets the requested load-balancing policy (<c>DOCKYARP_LB</c>), if any.</summary>
    public LoadBalancingPolicy? LoadBalancingPolicy { get; init; }

    /// <summary>Gets the Basic Auth credentials (<c>DOCKYARP_AUTH_*</c>), set only when complete.</summary>
    public BasicAuthCredentials? Auth { get; init; }
}
