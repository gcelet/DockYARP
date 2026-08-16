namespace DockYarp.App.Observability;

using DockYarp.AdminApi;

/// <summary>Maps the admin API and the Prometheus <c>/metrics</c> endpoint, scoped to <c>AdminApi:Host</c> when set.</summary>
public static class AdminEndpointMapping
{
    /// <summary>Maps <c>/api/*</c> and <c>/metrics</c>, scoping both to the configured admin host when one is set.</summary>
    /// <param name="app">The web application.</param>
    /// <remarks>When <c>AdminApi:Host</c> is set, both respond only on that host so a backend's <c>/api/*</c> is not shadowed on other hosts.</remarks>
    public static void MapDockYarpAdmin(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        AdminApiOptions options = app.Services.GetRequiredService<AdminApiOptions>();
        if (options.Surface == AdminApiSurface.Disabled)
        {
            return;
        }

        app.MapAdminApi(options.Host);

        IEndpointConventionBuilder metrics = app.MapPrometheusScrapingEndpoint();
        if (options.Host is { Length: > 0 } host)
        {
            metrics.RequireHost(host);
        }
    }
}
