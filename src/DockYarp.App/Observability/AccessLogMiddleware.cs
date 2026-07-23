namespace DockYarp.App.Observability;

using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>Emits a structured access-log entry for each handled request.</summary>
/// <param name="options">Access-log options (enable/disable, excluded prefixes).</param>
/// <param name="logger">Logger the access entry is written to.</param>
public sealed class AccessLogMiddleware(AccessLogOptions options, ILogger<AccessLogMiddleware> logger) : IMiddleware
{
    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!options.Enabled || IsExcluded(context.Request.Path))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        long start = Stopwatch.GetTimestamp();
        try
        {
            await next(context).ConfigureAwait(false);
        }
        finally
        {
            HttpRequest request = context.Request;
            AccessLog.Request(
                logger,
                request.Method,
                request.Scheme,
                request.Host.Host,
                request.Path,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
    }

    private bool IsExcluded(PathString path) =>
        Array.Exists(
            options.ExcludedPathPrefixes,
            prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
}
