namespace DockYarp.Tls;

using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

/// <summary>Serves ACME HTTP-01 challenge responses at <c>/.well-known/acme-challenge/{token}</c>.</summary>
/// <param name="store">The challenge store.</param>
public sealed class Http01ChallengeMiddleware(IHttp01ChallengeStore store) : IMiddleware
{
    private const string Prefix = "/.well-known/acme-challenge/";

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        string? path = context.Request.Path.Value;
        if (path is not null && path.StartsWith(Prefix, StringComparison.Ordinal))
        {
            string token = path[Prefix.Length..];
            if (store.TryGet(token, out string? keyAuthorization))
            {
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync(keyAuthorization).ConfigureAwait(false);
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
