namespace DockYarp.Dashboard;

using System.Collections.Immutable;
using System.Globalization;
using System.Text;

using DockYarp.AdminApi;
using DockYarp.Core.Interfaces;
using DockYarp.Core.Models;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Maps the read-only admin dashboard, scoped to <c>AdminApi:Host</c> when set.</summary>
public static class DashboardEndpointMapping
{
    private static readonly TimeSpan NearExpiryWindow = TimeSpan.FromDays(14);

    /// <summary>Maps the dashboard's endpoints, only when <see cref="AdminApiOptions.Surface"/> is
    /// <see cref="AdminApiSurface.ApiAndDashboard"/>, scoping them to the configured admin host when one is set.
    /// When <see cref="AdminApiOptions.AllowCertificateDownload"/> is also set, additionally maps the opt-in
    /// certificate/private-key download routes under the same host isolation.</summary>
    /// <param name="app">The web application.</param>
    /// <remarks>
    /// Mirrors <c>AdminEndpointMapping.MapDockYarpAdmin</c>'s host-isolation shape, kept as its own extension
    /// method (not folded into that one) so an auth requirement can be layered on this mapping later without
    /// touching the JSON admin API's. The download routes deliberately live here rather than under <c>/api/*</c>:
    /// they are reached by a plain browser link from <c>/dashboard</c>, and <c>/api/*</c> requires the admin API
    /// key header, which the dashboard never sends to the browser (see "Read-only admin dashboard").
    /// </remarks>
    public static void MapDockYarpDashboard(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        AdminApiOptions options = app.Services.GetRequiredService<AdminApiOptions>();
        if (options.Surface != AdminApiSurface.ApiAndDashboard)
        {
            return;
        }

        RequireHostIfConfigured(app.MapGet("/dashboard", GetDashboard), options.Host);
        RequireHostIfConfigured(app.MapPost("/dashboard/certs/{host}/convert", PostConvertAsync), options.Host);
        RequireHostIfConfigured(app.MapPost("/dashboard/certs/{host}/reencrypt", PostReencryptAsync), options.Host);
        RequireHostIfConfigured(app.MapPost("/dashboard/certs/{host}/revoke", PostRevokeAsync), options.Host);

        if (options.AllowCertificateDownload)
        {
            MapCertificateDownload(app, options.Host);
        }
    }

    private static IResult GetDashboard(HttpContext context, [AsParameters] DashboardServices services)
    {
        RouteConfigSnapshot snapshot = services.RouteConfigStore.Current;
        (string status, string discoveryStatus) = AdminMapper.ResolveHealth(services.DiscoveryHealth);
        DashboardViewModel model = new()
        {
            Routes = AdminMapper.Routes(snapshot),
            ClustersById = AdminMapper.Clusters(snapshot).ToImmutableDictionary(cluster => cluster.Id, StringComparer.Ordinal),
            Certificates = [.. services.CertificateInventory.List().Select(cert => ToCertificateRow(cert, services.CertificateConverter))],
            AllowCertificateDownload = services.AdminApiOptions.AllowCertificateDownload,
            AllowCertificateConversion = services.AdminApiOptions.AllowCertificateConversion,
            AllowKeyReencryption = services.AdminApiOptions.AllowCertificateConversion
                && services.CertificateConverter.PrivateKeyEncryptionConfigured,
            AllowCertificateRevocation = services.AdminApiOptions.AllowCertificateRevocation,
            Status = status,
            DiscoveryStatus = discoveryStatus,
            AntiforgeryToken = services.Antiforgery.GetAndStoreTokens(context).RequestToken ?? string.Empty,
        };

        return Results.RazorSlice<Slices.Dashboard, DashboardViewModel>(model);
    }

    /// <summary>The services <see cref="GetDashboard"/> needs, bundled via <see cref="AsParametersAttribute"/> to
    /// stay within the analyzer's 5-parameter limit on the handler delegate itself.</summary>
    /// <remarks>
    /// Internal, not private: ASP.NET Core's Request Delegate Generator (the AOT-safe compile-time endpoint
    /// generator) skips an endpoint referencing an inaccessible type (<c>RDG012</c>), falling back to
    /// reflection-based generation at runtime for that one endpoint — confirmed via a real
    /// <c>-p:PublishAot=true</c> spike, not assumed.
    /// </remarks>
    internal sealed record DashboardServices(
        IRouteConfigStore RouteConfigStore,
        ICertificateInventory CertificateInventory,
        IDiscoveryHealth DiscoveryHealth,
        ICertificateConverter CertificateConverter,
        AdminApiOptions AdminApiOptions,
        IAntiforgery Antiforgery);

    // Checks AllowCertificateConversion itself rather than trusting the view to have hidden the form —
    // defense in depth, since the option gates both whether the action renders and whether it is honored.
    private static async Task<IResult> PostConvertAsync(
        string host, HttpContext context, ICertificateConverter certificateConverter,
        AdminApiOptions adminApiOptions, IAntiforgery antiforgery)
    {
        // IsRequestValidAsync (non-throwing) rather than ValidateRequestAsync + catch: an invalid token is
        // an expected, routine outcome here, not an exceptional one.
        if (!await antiforgery.IsRequestValidAsync(context).ConfigureAwait(false))
        {
            return Results.BadRequest();
        }

        if (adminApiOptions.AllowCertificateConversion)
        {
            certificateConverter.ConvertToPem(host);
        }

        return Results.Redirect("/dashboard");
    }

    // Checks AllowKeyReencryption itself rather than trusting the view to have hidden the form — defense in
    // depth, same as PostConvertAsync.
    private static async Task<IResult> PostReencryptAsync(
        string host, HttpContext context, ICertificateConverter certificateConverter,
        AdminApiOptions adminApiOptions, IAntiforgery antiforgery)
    {
        // IsRequestValidAsync (non-throwing) rather than ValidateRequestAsync + catch: an invalid token is
        // an expected, routine outcome here, not an exceptional one.
        if (!await antiforgery.IsRequestValidAsync(context).ConfigureAwait(false))
        {
            return Results.BadRequest();
        }

        if (adminApiOptions.AllowCertificateConversion && certificateConverter.PrivateKeyEncryptionConfigured)
        {
            certificateConverter.ReencryptPrivateKey(host);
        }

        return Results.Redirect("/dashboard");
    }

    // Checks AllowCertificateRevocation itself rather than trusting the view to have hidden the form —
    // defense in depth, same as PostConvertAsync/PostReencryptAsync.
    private static async Task<IResult> PostRevokeAsync(
        string host, HttpContext context, ICertificateRevoker certificateRevoker,
        AdminApiOptions adminApiOptions, IAntiforgery antiforgery)
    {
        // IsRequestValidAsync (non-throwing) rather than ValidateRequestAsync + catch: an invalid token is
        // an expected, routine outcome here, not an exceptional one.
        if (!await antiforgery.IsRequestValidAsync(context).ConfigureAwait(false))
        {
            return Results.BadRequest();
        }

        if (adminApiOptions.AllowCertificateRevocation)
        {
            await certificateRevoker.RevokeCertificateAsync(host, context.RequestAborted).ConfigureAwait(false);
        }

        return Results.Redirect("/dashboard");
    }

    private static DashboardViewModel.CertificateRow ToCertificateRow(
        AdminApiModels.CertView cert, ICertificateConverter certificateConverter)
    {
        DateTimeOffset notAfter = DateTimeOffset.Parse(cert.NotAfter, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new DashboardViewModel.CertificateRow
        {
            Host = cert.Host,
            NotAfter = notAfter,
            NearExpiry = notAfter - DateTimeOffset.UtcNow <= NearExpiryWindow,
            IsPfxBacked = certificateConverter.IsPfxBacked(cert.Host),
            RequiresKeyReencryption = certificateConverter.RequiresKeyReencryption(cert.Host),
        };
    }

    private static void RequireHostIfConfigured(IEndpointConventionBuilder builder, string? host)
    {
        if (host is { Length: > 0 })
        {
            builder.RequireHost(host);
        }
    }

    private static void MapCertificateDownload(WebApplication app, string? host)
    {
        IEndpointConventionBuilder certificate = app.MapGet(
            "/dashboard/certs/{host}/certificate",
            (string host, ICertificateExporter exporter) => ExportResult(exporter, host, isPrivateKey: false));
        IEndpointConventionBuilder privateKey = app.MapGet(
            "/dashboard/certs/{host}/private-key",
            (string host, ICertificateExporter exporter) => ExportResult(exporter, host, isPrivateKey: true));

        RequireHostIfConfigured(certificate, host);
        RequireHostIfConfigured(privateKey, host);
    }

    private static IResult ExportResult(ICertificateExporter exporter, string host, bool isPrivateKey)
    {
        CertificateExport? export = exporter.Export(host);
        if (export is null)
        {
            return Results.NotFound();
        }

        (string pem, string extension) = isPrivateKey ? (export.PrivateKeyPem, "key") : (export.CertificatePem, "crt");
        return Results.File(Encoding.UTF8.GetBytes(pem), "application/x-pem-file", $"{host}.{extension}");
    }
}
