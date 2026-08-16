namespace DockYarp.Dashboard;

using DockYarp.AdminApi;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Maps the read-only admin dashboard, scoped to <c>AdminApi:Host</c> when set.</summary>
public static class DashboardEndpointMapping
{
    /// <summary>Maps the dashboard's Razor Pages, unless <see cref="AdminApiOptions.DashboardEnabled"/> is
    /// <see langword="false"/>, scoping them to the configured admin host when one is set.</summary>
    /// <param name="app">The web application.</param>
    /// <remarks>
    /// Mirrors <c>AdminEndpointMapping.MapDockYarpAdmin</c>'s host-isolation shape, kept as its own extension
    /// method (not folded into that one) so an auth requirement can be layered on this mapping later without
    /// touching the JSON admin API's.
    /// </remarks>
    public static void MapDockYarpDashboard(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        AdminApiOptions options = app.Services.GetRequiredService<AdminApiOptions>();
        if (!options.DashboardEnabled)
        {
            return;
        }

        IEndpointConventionBuilder dashboard = app.MapRazorPages();
        if (options.Host is { Length: > 0 } host)
        {
            dashboard.RequireHost(host);
        }
    }
}
