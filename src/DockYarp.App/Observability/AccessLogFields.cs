namespace DockYarp.App.Observability;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using Microsoft.AspNetCore.Http;

/// <summary>The access-log field catalog and the operator-selectable field projection.</summary>
/// <remarks>The <see cref="Select"/> projection is pure so it can be unit tested without an HTTP context.</remarks>
public static class AccessLogFields
{
    /// <summary>The field names available for selection, in catalog order.</summary>
    public static readonly ImmutableArray<string> Names =
    [
        "Method", "Scheme", "Host", "Path", "Query", "Protocol",
        "RemoteIp", "UserAgent", "Referer", "StatusCode", "ElapsedMs",
    ];

    /// <summary>Builds the full field catalog (name → value) for a handled request.</summary>
    /// <param name="context">The request context.</param>
    /// <param name="elapsedMs">The elapsed time in milliseconds.</param>
    /// <returns>The catalog entries in <see cref="Names"/> order.</returns>
    public static IReadOnlyList<KeyValuePair<string, object>> Build(HttpContext context, double elapsedMs)
    {
        ArgumentNullException.ThrowIfNull(context);
        HttpRequest request = context.Request;
        return
        [
            new("Method", request.Method),
            new("Scheme", request.Scheme),
            new("Host", request.Host.Host),
            new("Path", request.Path.Value ?? string.Empty),
            new("Query", request.QueryString.Value ?? string.Empty),
            new("Protocol", request.Protocol),
            new("RemoteIp", context.Connection.RemoteIpAddress?.ToString() ?? string.Empty),
            new("UserAgent", request.Headers.UserAgent.ToString()),
            new("Referer", request.Headers.Referer.ToString()),
            new("StatusCode", context.Response.StatusCode),
            new("ElapsedMs", elapsedMs),
        ];
    }

    /// <summary>Projects the catalog to the named fields, in order (canonical-cased; unknown names skipped).</summary>
    /// <param name="catalog">The full field catalog.</param>
    /// <param name="fields">The operator-selected field names.</param>
    /// <returns>The selected fields, in the requested order.</returns>
    public static IReadOnlyList<KeyValuePair<string, object>> Select(
        IReadOnlyList<KeyValuePair<string, object>> catalog, IReadOnlyList<string> fields)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(fields);

        List<KeyValuePair<string, object>> selected = new(fields.Count);
        foreach (string field in fields)
        {
            KeyValuePair<string, object> match = catalog.FirstOrDefault(
                entry => string.Equals(entry.Key, field, StringComparison.OrdinalIgnoreCase));
            if (match.Key is not null)
            {
                selected.Add(match);
            }
        }

        return selected;
    }

    /// <summary>Renders the selected fields as a compact <c>key=value</c> message.</summary>
    /// <param name="state">The selected fields.</param>
    /// <param name="exception">Unused; present for the logging formatter signature.</param>
    /// <returns>The rendered log message.</returns>
    public static string Format(IReadOnlyList<KeyValuePair<string, object>> state, Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(state);
        return string.Join(' ', state.Select(entry => string.Create(CultureInfo.InvariantCulture, $"{entry.Key}={entry.Value}")));
    }
}
