namespace DockYarp.App.Observability;

using System;
using System.Collections.Generic;
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
            double elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if (options.Fields is { Length: > 0 } fields)
            {
                // Operator-selected field template: emit exactly the configured fields, structured.
                IReadOnlyList<KeyValuePair<string, object>> selected =
                    AccessLogFields.Select(AccessLogFields.Build(context, elapsedMs), fields);
                logger.Log(LogLevel.Information, default, selected, null, AccessLogFields.Format);
            }
            else
            {
                HttpRequest request = context.Request;
                AccessLog.Request(
                    logger,
                    request.Method,
                    request.Scheme,
                    request.Host.Host,
                    request.Path,
                    context.Response.StatusCode,
                    elapsedMs);
            }
        }
    }

    private bool IsExcluded(PathString path) =>
        Array.Exists(
            options.ExcludedPathPrefixes,
            prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
}
