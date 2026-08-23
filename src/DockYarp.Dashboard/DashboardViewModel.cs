namespace DockYarp.Dashboard;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using DockYarp.AdminApi;

/// <summary>The read-only admin dashboard's rendering state, built fresh on every <c>GET /dashboard</c>.</summary>
/// <remarks>
/// A plain record, not a <c>PageModel</c>: RazorSlices templates are compiled types, not DI-activated ones, so
/// the endpoint handler gathers state from the injected services itself and passes it in, rather than the
/// template constructing itself from DI as a Razor Pages <c>PageModel</c> would.
/// </remarks>
public sealed record DashboardViewModel
{
    /// <summary>A certificate row with its expiry pre-parsed for the view (avoids re-parsing in Razor markup).</summary>
    public sealed record CertificateRow
    {
        /// <summary>Gets the certificate host.</summary>
        public required string Host { get; init; }

        /// <summary>Gets the expiry timestamp.</summary>
        public required DateTimeOffset NotAfter { get; init; }

        /// <summary>Gets a value indicating whether the certificate expires within the warning window.</summary>
        public required bool NearExpiry { get; init; }

        /// <summary>Gets a value indicating whether this certificate is currently backed by a legacy PFX file.</summary>
        public required bool IsPfxBacked { get; init; }

        /// <summary>Gets a value indicating whether this host's private key still needs re-encryption onto the
        /// currently configured passphrase.</summary>
        public required bool RequiresKeyReencryption { get; init; }
    }

    /// <summary>Gets the current routes, sanitized the same way the admin API exposes them.</summary>
    public required IReadOnlyList<AdminApiModels.RouteView> Routes { get; init; }

    /// <summary>Gets the current clusters, keyed by id for lookup from the routes table.</summary>
    public required IReadOnlyDictionary<string, AdminApiModels.ClusterView> ClustersById { get; init; } =
        ImmutableDictionary<string, AdminApiModels.ClusterView>.Empty;

    /// <summary>Gets the stored certificates, with expiry parsed and near-expiry flagged for display.</summary>
    public required IReadOnlyList<CertificateRow> Certificates { get; init; }

    /// <summary>Gets a value indicating whether the certificate table should render download links.</summary>
    public required bool AllowCertificateDownload { get; init; }

    /// <summary>Gets a value indicating whether the certificate table should render the PFX-to-PEM conversion
    /// action.</summary>
    public required bool AllowCertificateConversion { get; init; }

    /// <summary>Gets a value indicating whether the certificate table should render the key re-encryption
    /// action.</summary>
    public required bool AllowKeyReencryption { get; init; }

    /// <summary>Gets the overall health status (<c>Healthy</c> or <c>Degraded</c>).</summary>
    public required string Status { get; init; }

    /// <summary>Gets the Docker discovery status (<c>connected</c>/<c>disconnected</c>/<c>disabled</c>).</summary>
    public required string DiscoveryStatus { get; init; }

    /// <summary>Gets the anti-forgery request token embedded as a hidden field in the two POST forms.</summary>
    public required string AntiforgeryToken { get; init; }

    /// <summary>Gets the CSS badge class matching <see cref="DiscoveryStatus"/>, computed here rather than as a
    /// nested conditional in the Razor markup.</summary>
    public string DiscoveryBadgeClass => DiscoveryStatus switch
    {
        "connected" => "badge-ok",
        "disabled" => "badge-warn",
        _ => "badge-bad",
    };
}
