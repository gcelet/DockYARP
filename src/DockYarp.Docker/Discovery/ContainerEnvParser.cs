namespace DockYarp.Docker.Discovery;

using System;
using System.Collections.Generic;

/// <summary>Parses a container's <c>Config.Env</c> (<c>KEY=VALUE</c> entries) into a key/value map.</summary>
/// <remarks>Pure and side-effect free so the parsing can be unit tested without a Docker daemon.</remarks>
public static class ContainerEnvParser
{
    private static readonly IReadOnlyDictionary<string, string> Empty =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Parses <c>KEY=VALUE</c> environment entries into an ordinal-keyed map.</summary>
    /// <param name="env">The container's env entries (from <c>Config.Env</c>), or <see langword="null"/>.</param>
    /// <returns>The parsed map; entries without a key (no <c>=</c>, or a leading <c>=</c>) are skipped, and a
    /// value may itself contain <c>=</c> (split on the first only).</returns>
    public static IReadOnlyDictionary<string, string> Parse(IEnumerable<string>? env)
    {
        if (env is null)
        {
            return Empty;
        }

        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (string entry in env)
        {
            int separator = entry.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0)
            {
                result[entry[..separator]] = entry[(separator + 1)..];
            }
        }

        return result.Count > 0 ? result : Empty;
    }
}
