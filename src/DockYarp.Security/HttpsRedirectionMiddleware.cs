namespace DockYarp.Security;

using System.Threading.Tasks;

using DockYarp.Core.Models;

using Microsoft.AspNetCore.Http;

/// <summary>Applies a route's HTTPS method: redirects HTTP to HTTPS (when a certificate exists) and refuses HTTPS on HTTP-only hosts.</summary>
/// <param name="routes">Route lookup used to find the request's route.</param>
/// <param name="certificates">Certificate availability used to avoid redirecting before a certificate exists.</param>
public sealed class HttpsRedirectionMiddleware(RouteLookup routes, ICertificateAvailability certificates) : IMiddleware
{
    /// <inheritdoc />
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        HttpRequest request = context.Request;
        if (request.Host.Host is not { Length: > 0 } host
            || !routes.TryGetRoute(context, out RouteRule? route)
            || route.Tls is not { } tls)
        {
            return next(context);
        }

        // The host is served over HTTP only; refuse an HTTPS request rather than proxy it.
        if (request.IsHttps && tls.Method == HttpsMethod.NoHttps)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        // Redirect HTTP to HTTPS only when the method redirects and a certificate is available.
        if (!request.IsHttps && Redirects(tls.Method) && certificates.IsAvailable(host))
        {
            string target = $"https://{host}{request.PathBase}{request.Path}{request.QueryString}";
            context.Response.Redirect(target, permanent: true, preserveMethod: true);
            return Task.CompletedTask;
        }

        return next(context);
    }

    private static bool Redirects(HttpsMethod method) => method is HttpsMethod.Redirect or HttpsMethod.NoHttp;
}
