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
}
