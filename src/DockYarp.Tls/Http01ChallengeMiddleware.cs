namespace DockYarp.Tls;

using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

/// <summary>Serves ACME HTTP-01 challenge responses at <c>/.well-known/acme-challenge/{token}</c>.</summary>
/// <remarks>
/// The challenge is served by token, independent of host routing, so a not-yet-routed host is still answered;
/// the store only holds tokens DockYarp is provisioning. Serving is skipped when disabled via
/// <see cref="TlsOptions.Http01ChallengeEnabled"/>.
/// </remarks>
/// <param name="store">The challenge store.</param>
/// <param name="options">TLS options carrying the challenge-serving toggle.</param>
public sealed class Http01ChallengeMiddleware(IHttp01ChallengeStore store, TlsOptions options) : IMiddleware
{
    private const string Prefix = "/.well-known/acme-challenge/";

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        string? path = context.Request.Path.Value;
        if (path is not null && path.StartsWith(Prefix, StringComparison.Ordinal))
        {
            if (!options.Http01ChallengeEnabled)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            string token = path[Prefix.Length..];
            if (store.TryGet(token, out string? keyAuthorization))
            {
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync(keyAuthorization, context.RequestAborted).ConfigureAwait(false);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
            }

            return;
        }

        await next(context).ConfigureAwait(false);
    }
}
