namespace DockYarp.Tls;

/// <summary>Data-plane HTTP/HTTPS listen ports for the proxy endpoints.</summary>
/// <remarks>
/// DockYarp binds these explicitly so the HTTPS endpoint can attach a per-connection TLS handshake callback,
/// which bypasses <c>ConfigureHttpsDefaults</c>. The defaults match the non-root chiseled container convention
/// (<c>8080</c>/<c>8443</c>); the orchestrator maps host <c>80</c>/<c>443</c> onto them.
/// </remarks>
public sealed class ServerEndpointOptions
{
    /// <summary>Gets or sets the plaintext HTTP port (ACME HTTP-01 challenge + redirects). Default <c>8080</c>.</summary>
    public int HttpPort { get; set; } = 8080;

    /// <summary>Gets or sets the HTTPS port (per-SNI TLS). Default <c>8443</c>.</summary>
    public int HttpsPort { get; set; } = 8443;

    /// <summary>Gets or sets a value indicating whether edge connections begin with a PROXY protocol header.</summary>
    /// <remarks>
    /// Default <see langword="false"/>. Enable behind an L4 load balancer (NLB/HAProxy) so the real client
    /// address is recovered from the PROXY protocol (v1 or v2) instead of the balancer's.
    /// </remarks>
    public bool EnableProxyProtocol { get; set; }
}
