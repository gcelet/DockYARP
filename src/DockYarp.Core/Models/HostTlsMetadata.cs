namespace DockYarp.Core.Models;

/// <summary>Per-host TLS metadata carried by a route for downstream TLS and security capabilities.</summary>
public sealed record HostTlsMetadata
{
    /// <summary>Gets the host name a certificate should be obtained for.</summary>
    public required string CertificateHost { get; init; }

    /// <summary>Gets the contact email used when requesting the certificate.</summary>
    public string? ContactEmail { get; init; }

    /// <summary>Gets the HTTPS method controlling HTTP↔HTTPS behavior for this host.</summary>
    public HttpsMethod Method { get; init; } = HttpsMethod.Redirect;
}
