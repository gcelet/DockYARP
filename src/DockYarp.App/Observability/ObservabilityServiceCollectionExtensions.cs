namespace DockYarp.App.Observability;

using DockYarp.AdminApi;
using DockYarp.Dashboard;
using DockYarp.Tls;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Metrics;

/// <summary>Registers DockYarp's observability: Prometheus metrics, access logging, and the admin dashboard.</summary>
internal static class ObservabilityServiceCollectionExtensions
{
    /// <summary>Registers the metrics meter/exporter, the access-log options and middleware, and (when enabled)
    /// the admin dashboard's Razor Pages services.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration source for the <c>AccessLog</c> section.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddDockYarpObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AdminApiOptions adminApiOptions = new();
        configuration.GetSection("AdminApi").Bind(adminApiOptions);
        services.AddSingleton(adminApiOptions);
        services.AddSingleton<ICertificateInventory, CertificateInventoryAdapter>();
        services.AddDockYarpDashboard(adminApiOptions);

        // Contribute the admin host to TLS provisioning (opt-in via AdminApi:LetsEncrypt). Registered after
        // AddDockYarpTls's TryAdd default, so this implementation wins; harmless no-op when not opted in.
        services.AddSingleton<IReservedCertificateHosts, AdminReservedCertificateHosts>();

        AccessLogOptions accessLog = new();
        configuration.GetSection("AccessLog").Bind(accessLog);
        services.AddSingleton(accessLog);
        services.AddSingleton<AccessLogMiddleware>();

        services.AddSingleton<DockYarpMetrics>();
        services.AddOpenTelemetry().WithMetrics(metrics => metrics
            .AddMeter(DockYarpMetrics.MeterName)
            .AddPrometheusExporter());
        return services;
    }
}
