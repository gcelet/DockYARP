namespace DockYarp.Core.Routing;

using System;

/// <summary>Classifies a <c>VIRTUAL_HOST</c> pattern and matches request hosts against it.</summary>
/// <remarks>
/// Parsing (which classifies the pattern) happens once when routes are indexed; <see cref="Matches"/> runs on
/// the request path and allocates nothing. Supported forms: exact, leading wildcard <c>*.suffix</c> (any
/// subdomain depth), and trailing wildcard <c>prefix.*</c>.
/// </remarks>
public sealed class HostPattern
{
    private readonly string token;

    private HostPattern(HostPatternKind kind, string token)
    {
        Kind = kind;
        this.token = token;
    }

    /// <summary>Gets the classified kind of this pattern (used for match precedence).</summary>
    public HostPatternKind Kind { get; }

    /// <summary>Classifies a host pattern string.</summary>
    /// <param name="pattern">The <c>VIRTUAL_HOST</c> pattern.</param>
    /// <returns>The classified <see cref="HostPattern"/>.</returns>
    public static HostPattern Parse(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.StartsWith("*.", StringComparison.Ordinal))
        {
            // "*.suffix" -> the required host suffix, including the leading dot.
            return new HostPattern(HostPatternKind.LeadingWildcard, pattern[1..]);
        }

        if (pattern.EndsWith(".*", StringComparison.Ordinal))
        {
            // "prefix.*" -> the required host prefix, including the trailing dot.
            return new HostPattern(HostPatternKind.TrailingWildcard, pattern[..^1]);
        }

        return new HostPattern(HostPatternKind.Exact, pattern);
    }

    /// <summary>Reports whether the given request host matches this pattern.</summary>
    /// <param name="host">The request host (without port).</param>
    /// <returns><see langword="true"/> when the host matches.</returns>
    public bool Matches(string host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return Kind switch
        {
            HostPatternKind.Exact => string.Equals(host, token, StringComparison.OrdinalIgnoreCase),
            HostPatternKind.LeadingWildcard =>
                host.Length > token.Length && host.EndsWith(token, StringComparison.OrdinalIgnoreCase),
            HostPatternKind.TrailingWildcard =>
                host.Length > token.Length && host.StartsWith(token, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }
}
