namespace DockYarp.App.ErrorPages;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Registers custom error pages.</summary>
internal static class ErrorPagesServiceCollectionExtensions
{
    /// <summary>Binds <c>ErrorPages</c> options and registers the provider and middleware.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration source for the <c>ErrorPages</c> section.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddDockYarpErrorPages(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ErrorPagesOptions options = new();
        configuration.GetSection("ErrorPages").Bind(options);
        services.AddSingleton(options);
        services.AddSingleton<ErrorPageProvider>();
        services.AddSingleton<ErrorPageMiddleware>();
        return services;
    }
}
