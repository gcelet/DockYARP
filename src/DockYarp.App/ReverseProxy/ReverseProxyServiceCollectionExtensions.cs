namespace DockYarp.App.ReverseProxy;

using DockYarp.Core.Interfaces;
using DockYarp.Core.Stores;

using Microsoft.Extensions.DependencyInjection;

using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

/// <summary>Registers the YARP reverse proxy driven from the routing store.</summary>
internal static class ReverseProxyServiceCollectionExtensions
{
    /// <summary>Adds the in-memory YARP proxy, forwarded-header transforms, store bridge, and request limits.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="xForwardedAction">Action applied to the <c>X-Forwarded-*</c> headers.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddDockYarpReverseProxy(
        this IServiceCollection services,
        ForwardedTransformActions xForwardedAction)
    {
        services.AddSingleton<IRouteConfigStore, RouteConfigStore>();
        services.AddReverseProxy()
            .LoadFromMemory([], [])
            .AddTransforms(context => ForwardedHeadersTransform.Apply(context, xForwardedAction));
        services.AddHostedService<YarpConfigBridge>();
        services.AddSingleton<RequestBodySizeMiddleware>();
        return services;
    }
}
