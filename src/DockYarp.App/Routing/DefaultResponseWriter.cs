namespace DockYarp.App.Routing;

using System;
using System.Threading.Tasks;

using DockYarp.Core.Configuration;

using Microsoft.AspNetCore.Http;

/// <summary>Writes the default response for requests that match no route and no default host.</summary>
public static class DefaultResponseWriter
{
    private const string Sentinel = "￿";

    /// <summary>Writes the configured default response: a redirect when a location is set, else the status code.</summary>
    /// <param name="context">The request context.</param>
    /// <param name="options">The routing options carrying the default status and optional redirect target.</param>
    /// <returns>A completed task.</returns>
    public static Task WriteAsync(HttpContext context, RoutingOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        context.Response.StatusCode = options.DefaultResponseStatusCode;
        if (!string.IsNullOrEmpty(options.DefaultResponseLocation))
        {
            context.Response.Headers.Location = Substitute(options.DefaultResponseLocation, context.Request);
        }

        return Task.CompletedTask;
    }

    private static string Substitute(string template, HttpRequest request) =>
        template
            .Replace("$$", Sentinel, StringComparison.Ordinal)
            .Replace("$scheme", request.Scheme, StringComparison.Ordinal)
            .Replace("$host", request.Host.Value ?? string.Empty, StringComparison.Ordinal)
            .Replace("$request_uri", $"{request.Path}{request.QueryString}", StringComparison.Ordinal)
            .Replace(Sentinel, "$", StringComparison.Ordinal);
}
