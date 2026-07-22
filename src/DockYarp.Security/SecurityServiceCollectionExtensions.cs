namespace DockYarp.Security;

using System;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Dependency-injection registration for the security middleware.</summary>
public static class SecurityServiceCollectionExtensions
{
    /// <summary>Registers the route lookup, options, and security middlewares.</summary>
    /// <remarks>The caller must also register an <c>IRouteConfigStore</c>.</remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Security headers options.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddDockYarpSecurity(this IServiceCollection services, SecurityHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddSingleton<RouteLookup>();
        services.AddSingleton<SecurityHeadersMiddleware>();
        services.AddSingleton<HttpsRedirectionMiddleware>();
        services.AddSingleton<ClientCertificateMiddleware>();
        services.AddSingleton<BasicAuthMiddleware>();
        return services;
    }
}
