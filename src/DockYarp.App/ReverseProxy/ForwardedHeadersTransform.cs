namespace DockYarp.App.ReverseProxy;

using System;
using System.Globalization;
using System.Net;
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

            return ValueTask.CompletedTask;
        });
    }
}
