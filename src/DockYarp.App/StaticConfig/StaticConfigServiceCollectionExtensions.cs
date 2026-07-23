namespace DockYarp.App.StaticConfig;

using DockYarp.Core.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the static (file-based) configuration source.</summary>
internal static class StaticConfigServiceCollectionExtensions
{
    /// <summary>Binds <c>StaticConfig</c> options and registers the static configuration provider.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration source for the <c>StaticConfig</c> section.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddDockYarpStaticConfig(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        StaticConfigOptions options = new();
        configuration.GetSection("StaticConfig").Bind(options);
        services.AddSingleton(options);
        services.AddSingleton<IStaticConfigProvider, StaticConfigProvider>();
        return services;
    }
}
