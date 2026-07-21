namespace DockYarp.Core.Models;

/// <summary>Per-host TLS metadata carried by a route for downstream TLS and security capabilities.</summary>
public sealed record HostTlsMetadata
{
    /// <summary>Gets the host name a certificate should be obtained for.</summary>
    public required string CertificateHost { get; init; }

    /// <summary>Gets the contact email used when requesting the certificate.</summary>
    public string? ContactEmail { get; init; }

    /// <summary>Gets a value indicating whether HTTP requests for this host should be redirected to HTTPS.</summary>
    public bool EnforceHttps { get; init; }
}
