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
    private const string HstsOff = "off";

    /// <inheritdoc />
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        IHeaderDictionary headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = options.FrameOptions;
        headers["Referrer-Policy"] = options.ReferrerPolicy;

        // The built-in Kestrel `Server` header is disabled at the host; emit a configured value only when set.
        if (!string.IsNullOrEmpty(options.ServerHeader))
        {
            headers["Server"] = options.ServerHeader;
        }

        if (context.Request.IsHttps && ResolveHsts(context) is { } hsts)
        {
            headers["Strict-Transport-Security"] = hsts;
        }

        return next(context);
    }

    private string? ResolveHsts(HttpContext context)
    {
        // A per-host override wins: a value replaces the header, "off"/empty suppresses it for the host.
        if (routes.TryGetRoute(context, out RouteRule? route) && route.Tls?.Hsts is { } perHost)
        {
            return perHost.Length == 0 || string.Equals(perHost, HstsOff, StringComparison.OrdinalIgnoreCase)
                ? null
                : perHost;
        }

        return options.EnableHsts ? BuildGlobalHsts() : null;
    }

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
