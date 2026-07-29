namespace DockYarp.Docker.Discovery;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Builds Docker-native inclusion filters from configured discovery options.</summary>
public static class DockerFilters
{
    /// <summary>Converts a configured filter map into the Docker.DotNet filter representation.</summary>
    /// <param name="filters">Map of Docker filter key to accepted values (OR within a key, AND across keys).</param>
    /// <returns>The Docker filter dictionary, or <see langword="null"/> when nothing is filtered.</returns>
    /// <remarks>
    /// The result matches Docker's shape (<c>key → {value → include}</c>); the inner flag is always
    /// <see langword="true"/> (inclusion). Empty or whitespace keys and values are dropped.
    /// </remarks>
    public static IDictionary<string, IDictionary<string, bool>>? Build(
        IDictionary<string, IList<string>>? filters)
    {
        if (filters is not { Count: > 0 })
        {
            return null;
        }

        Dictionary<string, IDictionary<string, bool>> result = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, IList<string>> entry in filters)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value is null)
            {
                continue;
            }

            Dictionary<string, bool> values = new(StringComparer.Ordinal);
            foreach (string value in entry.Value.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                values[value] = true;
            }

            if (values.Count > 0)
            {
                result[entry.Key] = values;
            }
        }

        return result.Count > 0 ? result : null;
    }
}
