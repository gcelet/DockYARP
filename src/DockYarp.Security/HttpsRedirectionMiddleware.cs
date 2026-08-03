namespace DockYarp.Security;

using System.Globalization;
using System.Threading.Tasks;

using DockYarp.Core.Models;

using Microsoft.AspNetCore.Http;

/// <summary>Applies a route's HTTPS method: redirects HTTP to HTTPS, refuses HTTPS on HTTP-only or untrusted-default hosts.</summary>
/// <param name="routes">Route lookup used to find the request's route.</param>
/// <param name="certificates">Certificate availability used to avoid redirecting before a certificate exists.</param>
/// <param name="options">Security options carrying the default-certificate trust and HTTP-on-missing-cert policies.</param>
public sealed class HttpsRedirectionMiddleware(
    RouteLookup routes,
    ICertificateAvailability certificates,
    SecurityHeadersOptions options) : IMiddleware
{
    private const int DefaultHttpsPort = 443;

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

        bool certificateAvailable = certificates.IsAvailable(host);

        // TRUST_DEFAULT_CERT=false: refuse HTTPS to a host with no real certificate (served via the default one).
        if (request.IsHttps && !certificateAvailable && !options.TrustDefaultCert)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        }

        // Redirect HTTP to HTTPS when the method redirects; a missing certificate suppresses it unless
        // ENABLE_HTTP_ON_MISSING_CERT is disabled (then the redirect is forced regardless).
        if (!request.IsHttps
            && Redirects(tls.Method)
            && (certificateAvailable || !options.EnableHttpOnMissingCert))
        {
            // Use the host's external HTTPS port when configured (behind a non-standard published port); omit an
            // explicit port at the default 443 so the common case is unchanged.
            string authority = tls.ExternalHttpsPort is { } externalPort && externalPort != DefaultHttpsPort
                ? $"{host}:{externalPort.ToString(CultureInfo.InvariantCulture)}"
                : host;
            string target = $"https://{authority}{request.PathBase}{request.Path}{request.QueryString}";
            context.Response.Redirect(target, permanent: true, preserveMethod: true);
            return Task.CompletedTask;
        }

        return next(context);
    }

    private static bool Redirects(HttpsMethod method) => method is HttpsMethod.Redirect or HttpsMethod.NoHttp;
}
