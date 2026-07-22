namespace DockYarp.Core.Models;

/// <summary>A routing rule mapping a host (and optional path prefix) to a target cluster.</summary>
/// <remarks>Types here are owned by <c>DockYarp.Core</c> and never reference YARP configuration types.</remarks>
public sealed record RouteRule
{
    /// <summary>Gets the exact host (for example <c>app.local</c>) or single-level wildcard (for example <c>*.local</c>).</summary>
    public required string HostPattern { get; init; }

    /// <summary>Gets the optional path prefix; <see langword="null"/> or empty matches any path.</summary>
    public string? PathPrefix { get; init; }

    /// <summary>Gets the priority; higher values win when several rules match the same request.</summary>
    public int Priority { get; init; }

    /// <summary>Gets the identifier of the target <see cref="Cluster"/>.</summary>
    public required string ClusterId { get; init; }

    /// <summary>Gets the optional per-host TLS metadata.</summary>
    public HostTlsMetadata? Tls { get; init; }

    /// <summary>Gets the optional Basic Auth credentials protecting this route.</summary>
    public BasicAuthCredentials? Auth { get; init; }

    /// <summary>Gets the client-certificate (mutual TLS) requirement for this route.</summary>
    public ClientCertificateRequirement ClientCertificate { get; init; } = ClientCertificateRequirement.None;

    /// <summary>Gets the optional request transforms.</summary>
    public RouteTransforms? Transforms { get; init; }
}
