namespace DockYarp.Security;

using System;
using System.Globalization;
using System.Threading.Tasks;

using DockYarp.Core.Models;

using Microsoft.AspNetCore.Http;

/// <summary>Adds baseline security headers (and HSTS on HTTPS, with an optional per-host override) to responses.</summary>
/// <param name="options">Header configuration.</param>
/// <param name="routes">Route lookup used to resolve a per-host HSTS override.</param>
public sealed class SecurityHeadersMiddleware(SecurityHeadersOptions options, RouteLookup routes) : IMiddleware
{
    private const string OffValue = "off";

    /// <inheritdoc />
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        IHeaderDictionary headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = options.FrameOptions;
        headers["Referrer-Policy"] = options.ReferrerPolicy;

        RouteRule? route = routes.TryGetRoute(context, out RouteRule? matched) ? matched : null;

        // The built-in Kestrel `Server` header is disabled at the host; emit a configured value unless the host
        // opts out via SERVER_TOKENS=off (or empty).
        if (!string.IsNullOrEmpty(options.ServerHeader) && !ServerHeaderSuppressed(route))
        {
            headers["Server"] = options.ServerHeader;
        }

        if (context.Request.IsHttps && ResolveHsts(route) is { } hsts)
        {
            headers["Strict-Transport-Security"] = hsts;
        }

        return next(context);
    }

    private static bool ServerHeaderSuppressed(RouteRule? route) =>
        route?.ServerTokens is { } tokens && IsOff(tokens);

    private string? ResolveHsts(RouteRule? route)
    {
        // A per-host override wins: a value replaces the header, "off"/empty suppresses it for the host.
        if (route?.Tls?.Hsts is { } perHost)
        {
            return IsOff(perHost) ? null : perHost;
        }

        return options.EnableHsts ? BuildGlobalHsts() : null;
    }

    private static bool IsOff(string value) =>
        value.Length == 0 || string.Equals(value, OffValue, StringComparison.OrdinalIgnoreCase);

    private string BuildGlobalHsts()
    {
        long seconds = (long)options.HstsMaxAge.TotalSeconds;
        string value = string.Create(CultureInfo.InvariantCulture, $"max-age={seconds}");
        if (options.HstsIncludeSubDomains)
        {
            value += "; includeSubDomains";
        }

        return options.HstsPreload ? $"{value}; preload" : value;
    }
}
