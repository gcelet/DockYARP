namespace DockYarp.Dashboard.Pages.Dashboard;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

using DockYarp.AdminApi;
using DockYarp.Core.Interfaces;
using DockYarp.Core.Models;

using Microsoft.AspNetCore.Mvc.RazorPages;

/// <summary>Page model for the read-only admin dashboard at <c>/dashboard</c>.</summary>
/// <remarks>
/// Reads directly from the same sources <c>/api/*</c> uses (<see cref="IRouteConfigStore"/>,
/// <see cref="ICertificateInventory"/>, <see cref="IDiscoveryHealth"/>) — no HTTP call to the admin API, so no
/// admin API key is ever present in what is sent to the browser.
/// </remarks>
[SuppressMessage("Design", "MA0048:File name must match type name", Justification = "ASP.NET Core Razor Pages convention: Index.cshtml.cs is the code-behind for Index.cshtml and contains IndexModel; the framework's own naming rule, not a violation.")]
public sealed class IndexModel(
    IRouteConfigStore routeConfigStore,
    ICertificateInventory certificateInventory,
    IDiscoveryHealth discoveryHealth,
    AdminApiOptions adminApiOptions) : PageModel
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
    }

    private static readonly TimeSpan NearExpiryWindow = TimeSpan.FromDays(14);

    /// <summary>Gets the current routes, sanitized the same way the admin API exposes them.</summary>
    public IReadOnlyList<AdminApiModels.RouteView> Routes { get; private set; } = [];

    /// <summary>Gets the current clusters, keyed by id for lookup from the routes table.</summary>
    public IReadOnlyDictionary<string, AdminApiModels.ClusterView> ClustersById { get; private set; } =
        ImmutableDictionary<string, AdminApiModels.ClusterView>.Empty;

    /// <summary>Gets the stored certificates, with expiry parsed and near-expiry flagged for display.</summary>
    public IReadOnlyList<CertificateRow> Certificates { get; private set; } = [];

    /// <summary>Gets a value indicating whether the certificate table should render download links.</summary>
    public bool AllowCertificateDownload => adminApiOptions.AllowCertificateDownload;

    /// <summary>Gets the overall health status (<c>Healthy</c> or <c>Degraded</c>).</summary>
    public string Status { get; private set; } = "Healthy";

    /// <summary>Gets the Docker discovery status (<c>connected</c>/<c>disconnected</c>/<c>disabled</c>).</summary>
    public string DiscoveryStatus { get; private set; } = "disabled";

    /// <summary>Gets the CSS badge class matching <see cref="DiscoveryStatus"/>, computed here rather than as a
    /// nested conditional in the Razor markup.</summary>
    public string DiscoveryBadgeClass => DiscoveryStatus switch
    {
        "connected" => "badge-ok",
        "disabled" => "badge-warn",
        _ => "badge-bad",
    };

    /// <summary>Snapshots the current routing/certificate/health state for rendering.</summary>
    public void OnGet()
    {
        RouteConfigSnapshot snapshot = routeConfigStore.Current;
        Routes = AdminMapper.Routes(snapshot);
        ClustersById = AdminMapper.Clusters(snapshot).ToImmutableDictionary(cluster => cluster.Id, StringComparer.Ordinal);
        Certificates = [.. certificateInventory.List().Select(ToCertificateRow)];
        (Status, DiscoveryStatus) = AdminMapper.ResolveHealth(discoveryHealth);
    }

    private static CertificateRow ToCertificateRow(AdminApiModels.CertView cert)
    {
        DateTimeOffset notAfter = DateTimeOffset.Parse(cert.NotAfter, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new CertificateRow
        {
            Host = cert.Host,
            NotAfter = notAfter,
            NearExpiry = notAfter - DateTimeOffset.UtcNow <= NearExpiryWindow,
        };
    }
}
