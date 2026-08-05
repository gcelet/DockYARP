namespace DockYarp.App.ReverseProxy;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

/// <summary>Configures the forwarded headers added to proxied requests.</summary>
public static class ForwardedHeadersTransform
{
    /// <summary>Applies the forwarded-header transforms to all routes.</summary>
    /// <param name="context">The transform builder context.</param>
    /// <param name="xForwardedAction">
    /// Action applied to the <c>X-Forwarded-*</c> headers — <see cref="ForwardedTransformActions.Append"/>
    /// to trust and append client-supplied values, or <see cref="ForwardedTransformActions.Set"/> to replace them.
    /// </param>
    public static void Apply(TransformBuilderContext context, ForwardedTransformActions xForwardedAction)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.UseDefaultForwarders = false;
        context.AddXForwarded(xForwardedAction);
        context.AddOriginalHost(useOriginal: true);

        context.AddRequestTransform(static transformContext =>
        {
            IPAddress? remote = transformContext.HttpContext.Connection.RemoteIpAddress;
            if (remote is not null)
            {
                transformContext.ProxyRequest.Headers.Remove("X-Real-IP");
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-Real-IP", remote.ToString());
            }

            int localPort = transformContext.HttpContext.Connection.LocalPort;
            transformContext.ProxyRequest.Headers.Remove("X-Forwarded-Port");
            transformContext.ProxyRequest.Headers.TryAddWithoutValidation(
                "X-Forwarded-Port",
                localPort.ToString(CultureInfo.InvariantCulture));

            // httpoxy mitigation: never forward a client-supplied `Proxy` header to the backend.
            transformContext.ProxyRequest.Headers.Remove("Proxy");

            // X-Forwarded-Ssl mirrors the effective forwarded proto (so it respects downstream-proxy trust).
            bool https = IsForwardedHttps(transformContext.ProxyRequest.Headers, transformContext.HttpContext.Request);
            transformContext.ProxyRequest.Headers.Remove("X-Forwarded-Ssl");
            transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Ssl", https ? "on" : "off");

            // X-Original-URI: the original request line (before any route rewrite); always the real URI.
            HttpRequest request = transformContext.HttpContext.Request;
            transformContext.ProxyRequest.Headers.Remove("X-Original-URI");
            transformContext.ProxyRequest.Headers.TryAddWithoutValidation(
                "X-Original-URI", $"{request.PathBase}{request.Path}{request.QueryString}");

            return ValueTask.CompletedTask;
        });
    }

    // The first hop of X-Forwarded-Proto is the original client scheme (Append mode) or DockYarp's own scheme
    // (Set mode); falls back to the actual connection when the header is absent.
    private static bool IsForwardedHttps(HttpRequestHeaders headers, HttpRequest request)
    {
        if (headers.TryGetValues("X-Forwarded-Proto", out IEnumerable<string>? values)
            && values.FirstOrDefault() is { } proto)
        {
            int comma = proto.IndexOf(',');
            ReadOnlySpan<char> firstHop = comma >= 0 ? proto.AsSpan(0, comma) : proto;
            return firstHop.Trim().Equals("https", StringComparison.OrdinalIgnoreCase);
        }

        return request.IsHttps;
    }
}
