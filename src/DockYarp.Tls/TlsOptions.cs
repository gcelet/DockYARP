namespace DockYarp.Tls;

using System;

/// <summary>Options for TLS/ACME certificate provisioning.</summary>
public sealed class TlsOptions
{
    /// <summary>Gets or sets the directory holding certificate files (PFX).</summary>
    public string CertificateDirectory { get; set; } = "certs";

    /// <summary>Gets or sets the default ACME contact email used when a host declares none.</summary>
    public string? ContactEmail { get; set; }

    /// <summary>Gets or sets the ACME directory URI (defaults to the Let's Encrypt staging endpoint).</summary>
    public Uri AcmeDirectoryUri { get; set; } = new("https://acme-staging-v02.api.letsencrypt.org/directory");

    /// <summary>Gets or sets a value indicating whether the ACME terms of service are accepted.</summary>
    public bool AcceptTermsOfService { get; set; }

    /// <summary>Gets or sets how long before expiry a certificate is renewed.</summary>
    public TimeSpan RenewBeforeExpiry { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Gets or sets the interval between provisioning/renewal checks.</summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(12);

    /// <summary>Gets or sets the minimum accepted TLS version (default TLS 1.2).</summary>
    public TlsVersion MinimumTlsVersion { get; set; } = TlsVersion.Tls12;

    /// <summary>Gets or sets a named TLS posture preset (<c>Mozilla-Modern</c>/<c>Intermediate</c>/<c>Old</c>).</summary>
    /// <remarks>
    /// A preset sets the minimum TLS version and a default cipher-suite list; an explicit <see cref="CipherSuites"/>
    /// overrides the preset's ciphers. An unset or unrecognized value leaves the configured values unchanged.
    /// </remarks>
    public string? SslPolicy { get; set; }

    /// <summary>Gets or sets the enabled HTTP protocols for the endpoints (default <c>Http1AndHttp2</c>).</summary>
    public string HttpProtocols { get; set; } = "Http1AndHttp2";

    /// <summary>Gets or sets an optional cipher-suite allow-list (applied on Linux/macOS only).</summary>
    public string[]? CipherSuites { get; set; }

    /// <summary>Gets or sets the path to a client CA certificate (PEM) enabling mutual TLS; <see langword="null"/> disables it.</summary>
    public string? ClientCaCertificatePath { get; set; }
}
