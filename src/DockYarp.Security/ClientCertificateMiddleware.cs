namespace DockYarp.Security;

using System.Threading.Tasks;

using DockYarp.Core.Models;

using Microsoft.AspNetCore.Http;

/// <summary>Enforces the matched route's client-certificate (mutual TLS) requirement.</summary>
/// <remarks>Certificates are validated against the CA at the handshake; this only enforces presence per host.</remarks>
/// <param name="routes">Route lookup used to find the request's route.</param>
public sealed class ClientCertificateMiddleware(RouteLookup routes) : IMiddleware
{
    /// <inheritdoc />
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        HttpRequest request = context.Request;
        if (request.Host.Host is { Length: > 0 } host
            && routes.TryMatch(host, request.Path, out RouteRule? route)
            && route.ClientCertificate == ClientCertificateRequirement.Required
            && context.Connection.ClientCertificate is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        return next(context);
    }
}
