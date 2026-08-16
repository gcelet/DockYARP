namespace DockYarp.Dashboard;

using DockYarp.AdminApi;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the read-only admin dashboard's services.</summary>
public static class DashboardServiceCollectionExtensions
{
    /// <summary>Registers Razor Pages for the dashboard, unless <see cref="AdminApiOptions.DashboardEnabled"/> is
    /// <see langword="false"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The already-bound admin API options.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddDockYarpDashboard(this IServiceCollection services, AdminApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        if (options.DashboardEnabled)
        {
            services.AddRazorPages();
        }

        return services;
    }
}
