namespace DockYarp.Core.Models;

/// <summary>Per-host TLS metadata carried by a route for downstream TLS and security capabilities.</summary>
public sealed record HostTlsMetadata
{
    /// <summary>Gets the host name a certificate should be obtained for.</summary>
    public required string CertificateHost { get; init; }

    /// <summary>Gets the name of a shared certificate (<c>CERT_NAME</c>) pinned for this host, if any.</summary>
    /// <remarks>When set, SNI selection serves the certificate stored under this name and ACME does not provision the host.</remarks>
    public string? CertificateName { get; init; }

    /// <summary>Gets the contact email used when requesting the certificate.</summary>
    public string? ContactEmail { get; init; }

    /// <summary>Gets the HTTPS method controlling HTTP↔HTTPS behavior for this host.</summary>
    public HttpsMethod Method { get; init; } = HttpsMethod.Redirect;

    /// <summary>Gets the per-host HSTS override; <see langword="null"/> uses the global policy, <c>off</c> suppresses it.</summary>
    public string? Hsts { get; init; }
}
