namespace DockYarp.Security;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using DockYarp.Core.Models;

using Microsoft.AspNetCore.Http;

/// <summary>Enforces Basic Auth on routes protected by a label credential or an htpasswd file.</summary>
/// <param name="routes">Route lookup used to find the request's route.</param>
/// <param name="htpasswd">Store of file-based Basic Auth credentials.</param>
public sealed class BasicAuthMiddleware(RouteLookup routes, HtpasswdStore htpasswd) : IMiddleware
{
    private const string Scheme = "Basic ";

    /// <inheritdoc />
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (!routes.TryGetRoute(context, out RouteRule? route))
        {
            return next(context);
        }

        BasicAuthCredentials? label = route.Auth;
        IReadOnlyDictionary<string, string>? fileEntries = htpasswd.Find(route.HostPattern, route.PathPrefix);
        if (label is null && fileEntries is null)
        {
            return next(context);
        }

        if (TryParseCredentials(context.Request, out string user, out string password)
            && IsAuthorized(user, password, label, fileEntries))
        {
            return next(context);
        }

        Challenge(context.Response, label?.Realm);
        return Task.CompletedTask;
    }

    private static bool IsAuthorized(
        string user, string password, BasicAuthCredentials? label, IReadOnlyDictionary<string, string>? fileEntries)
    {
        if (label is not null && MatchesLabel(user, password, label))
        {
            return true;
        }

        return fileEntries is not null
            && fileEntries.TryGetValue(user, out string? hash)
            && HtpasswdVerifier.Verify(password, hash);
    }

    private static bool MatchesLabel(string user, string password, BasicAuthCredentials credentials)
    {
        // Evaluate both comparisons into locals first so combining them does not leak timing.
        bool userMatch = FixedTimeEquals(user, credentials.Username);
        bool passwordMatch = FixedTimeEquals(password, credentials.Password);
        return userMatch && passwordMatch;
    }

    private static bool TryParseCredentials(HttpRequest request, out string user, out string password)
    {
        user = string.Empty;
        password = string.Empty;

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

        user = decoded[..separator];
        password = decoded[(separator + 1)..];
        return true;
    }

    private static bool FixedTimeEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));

    private static void Challenge(HttpResponse response, string? realm)
    {
        response.StatusCode = StatusCodes.Status401Unauthorized;
        response.Headers.WWWAuthenticate = $"Basic realm=\"{realm ?? "DockYarp"}\", charset=\"UTF-8\"";
    }
}
