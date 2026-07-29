namespace DockYarp.Core.Routing;

using System;
using System.Collections.Concurrent;

/// <summary>Classifies a <c>VIRTUAL_HOST</c> pattern and matches request hosts against it.</summary>
/// <remarks>
/// Supported forms: exact, leading wildcard <c>*.suffix</c> (any subdomain depth), trailing wildcard
/// <c>prefix.*</c>, and a <c>~</c>-prefixed regular expression. Parsing (which classifies) is memoized; regex
/// compilation/caching and its ReDoS-bounded matching are delegated to <see cref="CompiledRegexCache"/>.
/// </remarks>
public sealed class HostPattern
{
    private static readonly ConcurrentDictionary<string, HostPattern> Cache = new(StringComparer.Ordinal);

    private readonly string token;

    private HostPattern(HostPatternKind kind, string token)
    {
        Kind = kind;
        this.token = token;
    }

    /// <summary>Gets the classified kind of this pattern (used for match precedence).</summary>
    public HostPatternKind Kind { get; }

    /// <summary>Classifies a host pattern string (memoized).</summary>
    /// <param name="pattern">The <c>VIRTUAL_HOST</c> pattern.</param>
    /// <returns>The classified <see cref="HostPattern"/>.</returns>
    public static HostPattern Parse(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return Cache.GetOrAdd(pattern, static value => Build(value));
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
            HostPatternKind.Regex => CompiledRegexCache.IsMatch(token, host),
            _ => false,
        };
    }

    private static HostPattern Build(string pattern)
    {
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

        if (pattern.StartsWith('~'))
        {
            // "~body" -> the regex body (compiled and matched by CompiledRegexCache).
            return new HostPattern(HostPatternKind.Regex, pattern[1..]);
        }

        return new HostPattern(HostPatternKind.Exact, pattern);
    }
}
