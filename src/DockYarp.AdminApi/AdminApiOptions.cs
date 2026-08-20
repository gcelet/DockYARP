namespace DockYarp.AdminApi;

/// <summary>Options for the admin API.</summary>
public sealed class AdminApiOptions
{
    /// <summary>Gets or sets the API key required in the <c>X-Api-Key</c> header. When null/empty the admin API is closed.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets the dedicated host the admin endpoints (<c>/api/*</c> and <c>/metrics</c>) are scoped to.</summary>
    /// <remarks>When set, those paths respond only on this host; on any other host they fall through to proxying (so a backend's <c>/api/*</c> is not shadowed). When null/empty, the admin endpoints answer on all hosts.</remarks>
    public string? Host { get; set; }

    /// <summary>Gets or sets a value indicating whether to ACME-provision a certificate for <see cref="Host"/>.</summary>
    /// <remarks>Opt-in (default <see langword="false"/>), mirroring <c>LETSENCRYPT_HOST</c> for routes. When enabled and <see cref="Host"/> is set, the admin host is provisioned and renewed like any vhost; otherwise it keeps the default/operator certificate.</remarks>
    public bool LetsEncrypt { get; set; }

    /// <summary>Gets or sets the ACME contact email for the admin host; falls back to <c>Tls:ContactEmail</c> when unset.</summary>
    public string? ContactEmail { get; set; }

    /// <summary>Gets or sets what the admin surface exposes.</summary>
    /// <remarks>Default <see cref="AdminApiSurface.Disabled"/> — <c>/api/*</c>, <c>/metrics</c>, and
    /// <c>/dashboard</c> are not mapped at all until this is set. Requires <see cref="Host"/> to be set for any
    /// value other than <see cref="AdminApiSurface.Disabled"/> — the application fails to start otherwise, since
    /// <see cref="Host"/> is also the dashboard's only trust boundary (it carries no application-level
    /// authentication of its own).</remarks>
    public AdminApiSurface Surface { get; set; }

    /// <summary>Gets or sets a value indicating whether the dashboard exposes certificate download links.</summary>
    /// <remarks>Opt-in (default <see langword="false"/>), mirroring <see cref="LetsEncrypt"/>'s pattern on this
    /// same options type. When enabled, a stored certificate's public chain and private key both become
    /// downloadable from <c>/dashboard</c>, protected only by the same network-isolation trust boundary
    /// (<see cref="Host"/>) as the rest of the admin surface — there is no additional application-level
    /// authentication. Has no effect unless <see cref="Surface"/> is <see cref="AdminApiSurface.ApiAndDashboard"/>.</remarks>
    public bool AllowCertificateDownload { get; set; }

    /// <summary>Gets or sets a value indicating whether the dashboard exposes the PFX-to-PEM certificate
    /// conversion action.</summary>
    /// <remarks>Opt-in (default <see langword="false"/>) — its own independent setting, deliberately not
    /// reusing <see cref="AllowCertificateDownload"/>: this is the first (and only) mutating action the admin
    /// surface exposes, gated on that basis rather than on secret-exposure risk (conversion doesn't expose any
    /// key material that wasn't already reachable via volume access — it only rewrites the on-disk format of an
    /// already-served certificate). Has no effect unless <see cref="Surface"/> is
    /// <see cref="AdminApiSurface.ApiAndDashboard"/>.</remarks>
    public bool AllowCertificateConversion { get; set; }
}
