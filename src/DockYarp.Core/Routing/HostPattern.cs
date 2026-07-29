namespace DockYarp.Core.Routing;

using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

/// <summary>Classifies a <c>VIRTUAL_HOST</c> pattern and matches request hosts against it.</summary>
/// <remarks>
/// Supported forms: exact, leading wildcard <c>*.suffix</c> (any subdomain depth), trailing wildcard
/// <c>prefix.*</c>, and a <c>~</c>-prefixed regular expression. Parsing (which classifies and, for regex,
/// compiles) is memoized, so it is cheap even when called per request; <see cref="Matches"/> allocates nothing.
/// </remarks>
public sealed class HostPattern
{
    private static readonly ConcurrentDictionary<string, HostPattern> Cache = new(StringComparer.Ordinal);
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    private readonly string token;
    private readonly Regex? regex;

    private HostPattern(HostPatternKind kind, string token, Regex? regex)
    {
        Kind = kind;
        this.token = token;
        this.regex = regex;
    }

    /// <summary>Gets the classified kind of this pattern (used for match precedence).</summary>
    public HostPatternKind Kind { get; }

    /// <summary>Classifies a host pattern string (memoized, so repeated calls do not recompile a regex).</summary>
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
            HostPatternKind.Regex => MatchesRegex(host),
            _ => false,
        };
    }

    private static HostPattern Build(string pattern)
    {
        if (pattern.StartsWith("*.", StringComparison.Ordinal))
        {
            // "*.suffix" -> the required host suffix, including the leading dot.
            return new HostPattern(HostPatternKind.LeadingWildcard, pattern[1..], regex: null);
        }

        if (pattern.EndsWith(".*", StringComparison.Ordinal))
        {
            // "prefix.*" -> the required host prefix, including the trailing dot.
            return new HostPattern(HostPatternKind.TrailingWildcard, pattern[..^1], regex: null);
        }

        if (pattern.StartsWith('~'))
        {
            return new HostPattern(HostPatternKind.Regex, pattern, TryCompile(pattern[1..]));
        }

        return new HostPattern(HostPatternKind.Exact, pattern, regex: null);
    }

    private static Regex? TryCompile(string body)
    {
        try
        {
            // A bounded match timeout guards against catastrophic backtracking (ReDoS).
            return new Regex(body, RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
        }
        catch (ArgumentException)
        {
            // An invalid expression compiles to a pattern that never matches (never crashes discovery).
            return null;
        }
    }

    private bool MatchesRegex(string host)
    {
        if (regex is null)
        {
            return false;
        }

        try
        {
            return regex.IsMatch(host);
        }
        catch (RegexMatchTimeoutException)
        {
            // Fail closed: a pathological expression/input never routes.
            return false;
        }
    }
}
