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

using Microsoft.AspNetCore.Mvc;
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
    ICertificateConverter certificateConverter,
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

        /// <summary>Gets a value indicating whether this certificate is currently backed by a legacy PFX file.</summary>
        public required bool IsPfxBacked { get; init; }

        /// <summary>Gets a value indicating whether this host's private key still needs re-encryption onto the
        /// currently configured passphrase.</summary>
        public required bool RequiresKeyReencryption { get; init; }
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

    /// <summary>Gets a value indicating whether the certificate table should render the PFX-to-PEM conversion
    /// action.</summary>
    public bool AllowCertificateConversion => adminApiOptions.AllowCertificateConversion;

    /// <summary>Gets a value indicating whether the certificate table should render the key re-encryption
    /// action.</summary>
    /// <remarks>
    /// Gated on <see cref="ICertificateConverter.PrivateKeyEncryptionConfigured"/> being <see langword="true"/>
    /// at all — not specifically on a "previous" passphrase — so it covers both a first-time enable (an
    /// existing plain key that has never been encrypted) and a rotation (an already-encrypted key that should
    /// move to the new passphrase). Rendered for every row rather than per-host, since there is no cheap way to
    /// tell from the dashboard which key is already under the current passphrase without decrypting it.
    /// </remarks>
    public bool AllowKeyReencryption =>
        adminApiOptions.AllowCertificateConversion && certificateConverter.PrivateKeyEncryptionConfigured;

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

    /// <summary>Converts a PFX-backed host's certificate to PEM, then redirects back to the dashboard
    /// (post-redirect-get, avoiding a resubmission prompt on refresh).</summary>
    /// <param name="host">The host to convert.</param>
    /// <returns>A redirect to <c>/dashboard</c>.</returns>
    /// <remarks>
    /// Checks <see cref="AllowCertificateConversion"/> itself rather than trusting the view to have hidden the
    /// form — defense in depth, since the option gates both whether the action renders and whether it is
    /// honored. Razor Pages validates the anti-forgery token on this handler automatically before it runs.
    /// </remarks>
    public IActionResult OnPostConvert(string host)
    {
        if (AllowCertificateConversion)
        {
            certificateConverter.ConvertToPem(host);
        }

        return RedirectToPage();
    }

    /// <summary>Rewrites a host's private key under the currently configured passphrase, then redirects back to
    /// the dashboard (post-redirect-get, avoiding a resubmission prompt on refresh).</summary>
    /// <param name="host">The host whose key should be re-encrypted.</param>
    /// <returns>A redirect to <c>/dashboard</c>.</returns>
    /// <remarks>
    /// Checks <see cref="AllowKeyReencryption"/> itself rather than trusting the view to have hidden the form —
    /// defense in depth, same as <see cref="OnPostConvert"/>. Razor Pages validates the anti-forgery token on
    /// this handler automatically before it runs.
    /// </remarks>
    public IActionResult OnPostReencrypt(string host)
    {
        if (AllowKeyReencryption)
        {
            certificateConverter.ReencryptPrivateKey(host);
        }

        return RedirectToPage();
    }

    private CertificateRow ToCertificateRow(AdminApiModels.CertView cert)
    {
        DateTimeOffset notAfter = DateTimeOffset.Parse(cert.NotAfter, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new CertificateRow
        {
            Host = cert.Host,
            NotAfter = notAfter,
            NearExpiry = notAfter - DateTimeOffset.UtcNow <= NearExpiryWindow,
            IsPfxBacked = certificateConverter.IsPfxBacked(cert.Host),
            RequiresKeyReencryption = certificateConverter.RequiresKeyReencryption(cert.Host),
        };
    }
}
