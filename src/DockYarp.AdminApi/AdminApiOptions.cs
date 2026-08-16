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

    /// <summary>Gets or sets a value indicating whether the read-only HTML dashboard at <c>/dashboard</c> is served.</summary>
    /// <remarks>Default <see langword="true"/>. The dashboard carries no application-level authentication of its
    /// own — it relies on the same network isolation as <see cref="Host"/>; set this to <see langword="false"/>
    /// to remove it entirely (no Razor Pages services registered, no route mapped) when that surface isn't wanted.</remarks>
    public bool DashboardEnabled { get; set; } = true;
}
