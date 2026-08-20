namespace DockYarp.Dashboard;

using System.Text;

using DockYarp.AdminApi;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Maps the read-only admin dashboard, scoped to <c>AdminApi:Host</c> when set.</summary>
public static class DashboardEndpointMapping
{
    /// <summary>Maps the dashboard's Razor Pages, only when <see cref="AdminApiOptions.Surface"/> is
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

        IEndpointConventionBuilder dashboard = app.MapRazorPages();
        if (options.Host is { Length: > 0 } host)
        {
            dashboard.RequireHost(host);
        }

        if (options.AllowCertificateDownload)
        {
            MapCertificateDownload(app, options.Host);
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

        if (host is { Length: > 0 })
        {
            certificate.RequireHost(host);
            privateKey.RequireHost(host);
        }
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
