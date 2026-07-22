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

    /// <summary>A certificate view (host and expiry; never the private key).</summary>
    /// <param name="Host">Certificate host.</param>
    /// <param name="NotAfter">Expiry timestamp (ISO-8601).</param>
    public sealed record CertView(string Host, string NotAfter);
}
