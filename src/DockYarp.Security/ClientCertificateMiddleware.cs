namespace DockYarp.Security;

using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using DockYarp.Core.Models;
using DockYarp.Tls;

using Microsoft.AspNetCore.Http;

/// <summary>Enforces the matched route's client-certificate (mutual TLS) requirement and computes the
/// connection's verification status for downstream header forwarding.</summary>
/// <remarks>
/// A <c>Required</c> host's certificate is already strictly validated at the handshake (an invalid/revoked one
/// never reaches this middleware); an <c>Optional</c> host's handshake accepts any certificate, so this
/// middleware re-validates (CA chain + CRL, via <see cref="ClientCertificateValidator"/>) to determine the real
/// outcome. The computed <see cref="ClientCertificateVerificationStatus"/> is stored on
/// <see cref="HttpContext.Items"/> under <see cref="VerificationStatusKey"/> for a route with a `Required`/
/// `Optional` requirement, so <c>ForwardedHeadersTransform</c> can forward it without re-validating.
/// </remarks>
/// <param name="routes">Route lookup used to find the request's route.</param>
/// <param name="clientCertificates">Validator for client certificates (CA chain + CRL).</param>
public sealed class ClientCertificateMiddleware(RouteLookup routes, ClientCertificateValidator clientCertificates) : IMiddleware
{
    /// <summary>The <see cref="HttpContext.Items"/> key under which the computed verification status is stored.</summary>
    public static readonly object VerificationStatusKey = new();

    /// <inheritdoc />
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (routes.TryGetRoute(context, out RouteRule? route) && route.ClientCertificate != ClientCertificateRequirement.None)
        {
            ClientCertificateVerificationStatus status = context.Connection.ClientCertificate switch
            {
                null => ClientCertificateVerificationStatus.NotPresented,
                X509Certificate2 certificate => clientCertificates.Validate(certificate)
                    ? ClientCertificateVerificationStatus.Verified
                    : ClientCertificateVerificationStatus.Failed,
            };
            context.Items[VerificationStatusKey] = status;

            if (route.ClientCertificate == ClientCertificateRequirement.Required
                && status != ClientCertificateVerificationStatus.Verified)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
        }

        return next(context);
    }
}
