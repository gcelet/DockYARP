using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

// Minimal echo backend for the end-to-end suite: it reflects the request (method, path, host, the local
// port that served it, request headers, and the received body size) as JSON. This covers the scenarios the
// off-the-shelf `traefik/whoami` image cannot: per-port identification (VIRTUAL_HOST_MULTIPORTS), a slow
// endpoint (DOCKYARP_PROXY_TIMEOUT) and body-size accounting (DOCKYARP_MAX_BODY_SIZE).
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

app.Run(async context =>
{
    HttpRequest request = context.Request;

    // A slow endpoint lets a test drive the proxy request timeout: /slow?ms=NNN delays before responding.
    if (request.Path.StartsWithSegments("/slow", StringComparison.OrdinalIgnoreCase)
        && int.TryParse(request.Query["ms"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int delayMs)
        && delayMs > 0)
    {
        await Task.Delay(delayMs, context.RequestAborted);
    }

    long bodyBytes = 0;
    if (request.ContentLength is > 0 || request.Headers.ContainsKey("Transfer-Encoding"))
    {
        byte[] buffer = new byte[8192];
        int read;
        while ((read = await request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
        {
            bodyBytes += read;
        }
    }

    Dictionary<string, string> headers = request.Headers.ToDictionary(
        header => header.Key,
        header => header.Value.ToString(),
        StringComparer.OrdinalIgnoreCase);

    var payload = new
    {
        // Identifies which backend container served the request (used by priority/default-host scenarios).
        id = Environment.GetEnvironmentVariable("BACKEND_ID"),
        method = request.Method,
        path = request.Path.Value ?? "/",
        host = request.Host.Value ?? string.Empty,
        port = context.Connection.LocalPort,
        bodyBytes,
        headers,
    };

    await context.Response.WriteAsJsonAsync(payload, context.RequestAborted);
});

await app.RunAsync();
