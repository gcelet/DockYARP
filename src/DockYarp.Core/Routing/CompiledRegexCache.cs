namespace DockYarp.Core.Routing;

using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

/// <summary>Compiles and caches regular expressions with a bounded match timeout (ReDoS-safe, fail-closed).</summary>
/// <remarks>
/// Used for the regex forms of host and path matching. Expressions are compiled once and cached by their body,
/// so a per-request <see cref="IsMatch"/> is a dictionary lookup rather than a recompile. A timeout or an invalid
/// expression yields no match (never throws to the caller, never stalls the request path).
/// </remarks>
public static class CompiledRegexCache
{
    private static readonly ConcurrentDictionary<string, Regex?> Cache = new(StringComparer.Ordinal);
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>Reports whether the input matches the (compiled, cached) expression.</summary>
    /// <param name="pattern">The regular-expression body.</param>
    /// <param name="input">The string to test.</param>
    /// <returns><see langword="true"/> on a match; <see langword="false"/> on no match, an invalid expression, or a timeout.</returns>
    public static bool IsMatch(string pattern, string input)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(input);

        Regex? regex = Cache.GetOrAdd(pattern, static body => Compile(body));
        if (regex is null)
        {
            return false;
        }

        try
        {
            return regex.IsMatch(input);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static Regex? Compile(string pattern)
    {
        try
        {
            // A bounded match timeout guards against catastrophic backtracking (ReDoS).
            return new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, MatchTimeout);
        }
        catch (ArgumentException)
        {
            // An invalid expression compiles to nothing and thus never matches.
            return null;
        }
    }
}
