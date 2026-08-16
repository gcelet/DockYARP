namespace DockYarp.Dashboard;

using DockYarp.AdminApi;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the read-only admin dashboard's services.</summary>
public static class DashboardServiceCollectionExtensions
{
    /// <summary>Registers Razor Pages for the dashboard, only when <see cref="AdminApiOptions.Surface"/> is
    /// <see cref="AdminApiSurface.ApiAndDashboard"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The already-bound admin API options.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddDockYarpDashboard(this IServiceCollection services, AdminApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        if (options.Surface == AdminApiSurface.ApiAndDashboard)
        {
            services.AddRazorPages();
        }

        return services;
    }
}
