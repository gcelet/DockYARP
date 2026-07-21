namespace DockYarp.Docker;

using System;

using DockYarp.Docker.Discovery;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Dependency-injection registration for Docker discovery.</summary>
public static class DockerDiscoveryServiceCollectionExtensions
{
    /// <summary>Registers Docker discovery (container source, reconciler, and hosted service).</summary>
    /// <remarks>The caller must also register an <c>IRouteConfigStore</c>.</remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Discovery options.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddDockerDiscovery(this IServiceCollection services, DockerDiscoveryOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddSingleton<IContainerSource, DockerContainerSource>();
        services.AddSingleton<DiscoveryReconciler>();
        services.AddHostedService<DockerDiscoveryService>();
        return services;
    }
}
