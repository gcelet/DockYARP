namespace DockYarp.Security;

using System;

/// <summary>Options for the security headers applied to responses.</summary>
public sealed class SecurityHeadersOptions
{
    /// <summary>Gets or sets a value indicating whether HSTS is emitted on HTTPS responses.</summary>
    public bool EnableHsts { get; set; } = true;

    /// <summary>Gets or sets the HSTS max-age.</summary>
    public TimeSpan HstsMaxAge { get; set; } = TimeSpan.FromDays(365);

    /// <summary>Gets or sets a value indicating whether HSTS applies to subdomains.</summary>
    public bool HstsIncludeSubDomains { get; set; }

    /// <summary>Gets or sets a value indicating whether the HSTS <c>preload</c> directive is emitted.</summary>
    public bool HstsPreload { get; set; }

    /// <summary>Gets or sets the <c>X-Frame-Options</c> value.</summary>
    public string FrameOptions { get; set; } = "DENY";

    /// <summary>Gets or sets the <c>Referrer-Policy</c> value.</summary>
    public string ReferrerPolicy { get; set; } = "no-referrer";

    /// <summary>Gets or sets the <c>Server</c> response header value; <see langword="null"/> or empty suppresses it.</summary>
    /// <remarks>The built-in Kestrel <c>Server</c> header is disabled at the host; this value, when set, is emitted instead.</remarks>
    public string? ServerHeader { get; set; }
}
