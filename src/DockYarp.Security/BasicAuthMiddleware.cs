namespace DockYarp.Security;

using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using DockYarp.Core.Models;

using Microsoft.AspNetCore.Http;

/// <summary>Enforces Basic Auth on routes that carry credentials.</summary>
/// <param name="routes">Route lookup used to find the request's route.</param>
public sealed class BasicAuthMiddleware(RouteLookup routes) : IMiddleware
{
    private const string Scheme = "Basic ";

    /// <inheritdoc />
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        HttpRequest request = context.Request;
        if (request.Host.Host is { Length: > 0 } host
            && routes.TryMatch(host, request.Path, out RouteRule? route)
            && route.Auth is { } credentials
            && !IsAuthorized(request, credentials))
        {
            Challenge(context.Response, credentials.Realm);
            return Task.CompletedTask;
        }

        return next(context);
    }

    private static bool IsAuthorized(HttpRequest request, BasicAuthCredentials credentials)
    {
        string? header = request.Headers.Authorization;
        if (string.IsNullOrEmpty(header) || !header.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header[Scheme.Length..].Trim()));
        }
        catch (FormatException)
        {
            return false;
        }

        int separator = decoded.IndexOf(':', StringComparison.Ordinal);
        if (separator < 0)
        {
            return false;
        }

        // Both comparisons are evaluated into locals first, so combining them does not leak timing.
        bool user = FixedTimeEquals(decoded[..separator], credentials.Username);
        bool password = FixedTimeEquals(decoded[(separator + 1)..], credentials.Password);
        return user && password;
    }

    private static bool FixedTimeEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));

    private static void Challenge(HttpResponse response, string? realm)
    {
        response.StatusCode = StatusCodes.Status401Unauthorized;
        response.Headers.WWWAuthenticate = $"Basic realm=\"{realm ?? "DockYarp"}\", charset=\"UTF-8\"";
    }
}
