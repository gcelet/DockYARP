namespace DockYarp.Security;

using System.Threading.Tasks;

using DockYarp.Core.Models;

using Microsoft.AspNetCore.Http;

/// <summary>Redirects HTTP requests to HTTPS when the matched route enforces it.</summary>
/// <param name="routes">Route lookup used to find the request's route.</param>
public sealed class HttpsRedirectionMiddleware(RouteLookup routes) : IMiddleware
{
    /// <inheritdoc />
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        HttpRequest request = context.Request;
        if (!request.IsHttps
            && request.Host.Host is { Length: > 0 } host
            && routes.TryMatch(host, request.Path, out RouteRule? route)
            && route.Tls is { EnforceHttps: true })
        {
            string target = $"https://{host}{request.PathBase}{request.Path}{request.QueryString}";
            context.Response.Redirect(target, permanent: true, preserveMethod: true);
            return Task.CompletedTask;
        }

        return next(context);
    }
}
