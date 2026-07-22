namespace DockYarp.Security;

using System.Threading.Tasks;

using DockYarp.Core.Models;

using Microsoft.AspNetCore.Http;

/// <summary>Redirects HTTP requests to HTTPS when the matched route's method requires it and a certificate exists.</summary>
/// <param name="routes">Route lookup used to find the request's route.</param>
/// <param name="certificates">Certificate availability used to avoid redirecting before a certificate exists.</param>
public sealed class HttpsRedirectionMiddleware(RouteLookup routes, ICertificateAvailability certificates) : IMiddleware
{
    /// <inheritdoc />
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        HttpRequest request = context.Request;
        if (!request.IsHttps
            && request.Host.Host is { Length: > 0 } host
            && routes.TryMatch(host, request.Path, out RouteRule? route)
            && route.Tls is { } tls
            && Redirects(tls.Method)
            && certificates.IsAvailable(host))
        {
            string target = $"https://{host}{request.PathBase}{request.Path}{request.QueryString}";
            context.Response.Redirect(target, permanent: true, preserveMethod: true);
            return Task.CompletedTask;
        }

        return next(context);
    }

    private static bool Redirects(HttpsMethod method) => method is HttpsMethod.Redirect or HttpsMethod.NoHttp;
}
