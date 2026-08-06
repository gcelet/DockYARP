namespace DockYarp.AdminApi;

using System.Collections.Generic;

/// <summary>Sanitized read models returned by the admin API (no secrets).</summary>
public static class AdminApiModels
{
    /// <summary>Per-host TLS view.</summary>
    public sealed record TlsView
    {
        /// <summary>Gets the host a certificate is requested for.</summary>
        public required string CertificateHost { get; init; }

        /// <summary>Gets the HTTPS method (<c>redirect</c>/<c>noredirect</c>/<c>nohttp</c>/<c>nohttps</c>).</summary>
        public required string HttpsMethod { get; init; }
    }

    /// <summary>A route view; exposes whether auth is required but never the credentials.</summary>
    public sealed record RouteView
    {
        /// <summary>Gets the host pattern.</summary>
        public required string Host { get; init; }

        /// <summary>Gets the optional path prefix.</summary>
        public string? PathPrefix { get; init; }

        /// <summary>Gets the route priority.</summary>
        public int Priority { get; init; }

        /// <summary>Gets the target cluster id.</summary>
        public required string ClusterId { get; init; }

        /// <summary>Gets a value indicating whether the route is protected by Basic Auth.</summary>
        public bool RequiresAuth { get; init; }

        /// <summary>Gets the optional TLS view.</summary>
        public TlsView? Tls { get; init; }
    }

    /// <summary>A cluster endpoint view.</summary>
    /// <param name="Id">Endpoint id.</param>
    /// <param name="Address">Destination address.</param>
    public sealed record EndpointView(string Id, string Address);

    /// <summary>A cluster view.</summary>
    /// <param name="Id">Cluster id.</param>
    /// <param name="LoadBalancingPolicy">Load-balancing policy.</param>
    /// <param name="Endpoints">Cluster endpoints.</param>
    public sealed record ClusterView(string Id, string LoadBalancingPolicy, IReadOnlyList<EndpointView> Endpoints);

    /// <summary>Overall health view.</summary>
    /// <param name="Status">Health status (<c>Healthy</c> or <c>Degraded</c>).</param>
    /// <param name="Routes">Active route count.</param>
    /// <param name="Clusters">Active cluster count.</param>
    /// <param name="Certificates">Stored certificate count.</param>
    /// <param name="Discovery">Docker discovery status (<c>connected</c>/<c>disconnected</c>/<c>disabled</c>).</param>
    public sealed record HealthView(string Status, int Routes, int Clusters, int Certificates, string Discovery);

    /// <summary>The running build's version.</summary>
    /// <param name="Version">The informational version (git-derived, including the commit id when available).</param>
    public sealed record VersionView(string Version);

    /// <summary>A certificate view (host and expiry; never the private key).</summary>
    /// <param name="Host">Certificate host.</param>
    /// <param name="NotAfter">Expiry timestamp (ISO-8601).</param>
    public sealed record CertView(string Host, string NotAfter);

    /// <summary>A route's transforms view.</summary>
    /// <param name="PathRemovePrefix">Prefix stripped before forwarding, if any.</param>
    /// <param name="PathAddPrefix">Prefix prepended after stripping, if any.</param>
    /// <param name="ResponseHeaders">Response headers set by overrides, if any.</param>
    public sealed record TransformsView(
        string? PathRemovePrefix, string? PathAddPrefix, IReadOnlyDictionary<string, string>? ResponseHeaders);

    /// <summary>A route's security policy view (no secrets).</summary>
    public sealed record SecurityView
    {
        /// <summary>Gets a value indicating whether the route is restricted to internal networks.</summary>
        public bool InternalOnly { get; init; }

        /// <summary>Gets the client-certificate requirement (<c>None</c>/<c>Optional</c>/<c>Required</c>).</summary>
        public required string ClientCertificate { get; init; }

        /// <summary>Gets the per-route maximum request body size in bytes, if any.</summary>
        public long? MaxRequestBodySize { get; init; }
    }

    /// <summary>The effective configuration resolved for a host/path (the DockYarp analog of a debug dump).</summary>
    /// <param name="Route">The matched route view.</param>
    /// <param name="Transforms">The route's transforms, if any.</param>
    /// <param name="Security">The route's security policy.</param>
    /// <param name="Cluster">The resolved target cluster, if present.</param>
    public sealed record ResolveView(
        RouteView Route, TransformsView? Transforms, SecurityView Security, ClusterView? Cluster);
}
