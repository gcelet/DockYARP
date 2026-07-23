namespace DockYarp.App.ReverseProxy;

using System.Threading.Tasks;

using DockYarp.Core.Models;
using DockYarp.Security;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

/// <summary>Applies the matched route's maximum request body size before the request is proxied.</summary>
/// <remarks>The limit is per-route; Kestrel's own limit is global, so it is set per request here.</remarks>
/// <param name="routes">Route lookup used to find the request's route.</param>
public sealed class RequestBodySizeMiddleware(RouteLookup routes) : IMiddleware
{
    /// <inheritdoc />
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (routes.TryGetRoute(context, out RouteRule? route)
            && route.MaxRequestBodySize is { } maxBytes
            && context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature)
        {
            feature.MaxRequestBodySize = maxBytes;
        }

        return next(context);
    }
}
