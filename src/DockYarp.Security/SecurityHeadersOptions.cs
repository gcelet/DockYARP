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

    /// <summary>Gets or sets the CIDR ranges considered "internal" for <c>NETWORK_ACCESS=internal</c> routes.</summary>
    /// <remarks>Defaults to the common private ranges plus IPv6 loopback; a client outside these is denied (403).</remarks>
    public string[] InternalRanges { get; set; } =
        ["127.0.0.0/8", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "::1/128"];

    /// <summary>Gets or sets the directory of Apache htpasswd files enabling file-based Basic Auth.</summary>
    /// <remarks>
    /// A file named <c>&lt;host&gt;</c> protects that vhost; <c>&lt;host&gt;_&lt;sha1hex(path)&gt;</c> protects a
    /// specific path. <see langword="null"/> or empty disables file-based Basic Auth.
    /// </remarks>
    public string? HtpasswdDirectory { get; set; }
}
