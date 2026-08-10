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

    /// <summary>Gets the per-host TLS preset (<c>SSL_POLICY</c>) overriding the global posture, if any.</summary>
    /// <remarks>When set to a recognized preset, the SNI handshake for this host uses that preset's version and ciphers.</remarks>
    public string? SslPolicy { get; init; }

    /// <summary>Gets the per-host frontend HTTP/2 toggle (<c>DOCKYARP_HTTP2</c>); <see langword="null"/> uses the global protocols.</summary>
    /// <remarks>When <see langword="false"/>, the SNI handshake for this host advertises only HTTP/1.1 via ALPN.</remarks>
    public bool? Http2Enabled { get; init; }

    /// <summary>Gets the external HTTPS port (<c>EXTERNAL_HTTPS_PORT</c>) used in the HTTP→HTTPS redirect target, if any.</summary>
    public int? ExternalHttpsPort { get; init; }

    /// <summary>Gets the per-host <c>ENABLE_HTTP_ON_MISSING_CERT</c> override; <see langword="null"/> uses the global default.</summary>
    public bool? EnableHttpOnMissingCert { get; init; }

    /// <summary>Gets the per-host <c>TRUST_DEFAULT_CERT</c> override; <see langword="null"/> uses the global default.</summary>
    public bool? TrustDefaultCert { get; init; }
}
