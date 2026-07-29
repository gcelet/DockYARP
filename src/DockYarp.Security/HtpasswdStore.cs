namespace DockYarp.Security;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/// <summary>Loads Apache htpasswd files (per host and per path) for file-based Basic Auth.</summary>
/// <remarks>
/// Files are read once at construction. A file named <c>&lt;host&gt;</c> protects the whole vhost;
/// <c>&lt;host&gt;_&lt;sha1hex(path)&gt;</c> protects a specific path (nginx-proxy's naming scheme).
/// </remarks>
public sealed class HtpasswdStore
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> files = new(StringComparer.Ordinal);

    /// <summary>Loads the htpasswd files from the configured directory, if any.</summary>
    /// <param name="options">Security options carrying the htpasswd directory.</param>
    public HtpasswdStore(SecurityHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Load(options.HtpasswdDirectory);
    }

    /// <summary>Finds the htpasswd entries governing a route, preferring a path-scoped file over the host file.</summary>
    /// <param name="host">The route host.</param>
    /// <param name="pathPrefix">The route path prefix, if any.</param>
    /// <returns>The user→hash entries protecting the route, or <see langword="null"/> when none apply.</returns>
    public IReadOnlyDictionary<string, string>? Find(string host, string? pathPrefix)
    {
        if (pathPrefix is { Length: > 0 } path
            && !string.Equals(path, "/", StringComparison.Ordinal)
            && files.TryGetValue($"{host}_{PathHash(path)}", out IReadOnlyDictionary<string, string>? scoped))
        {
            return scoped;
        }

        return files.GetValueOrDefault(host);
    }

    private void Load(string? directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(directory))
        {
            Dictionary<string, string> entries = new(StringComparer.Ordinal);
            foreach (string line in File.ReadLines(file))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#')
                {
                    continue;
                }

                int separator = trimmed.IndexOf(':', StringComparison.Ordinal);
                if (separator > 0)
                {
                    entries[trimmed[..separator]] = trimmed[(separator + 1)..];
                }
            }

            files[Path.GetFileName(file)] = entries;
        }
    }

    // SHA1 here only derives the per-path file name (nginx-proxy's scheme); it is not a security primitive.
    [SuppressMessage(
        "Security",
        "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "The per-path htpasswd file name is SHA1(path) by nginx-proxy convention; not security-relevant.")]
    [SuppressMessage(
        "Minor Code Smell",
        "S4790:Using weak hashing algorithms is security-sensitive",
        Justification = "The per-path htpasswd file name is SHA1(path) by nginx-proxy convention; not security-relevant.")]
    private static string PathHash(string path) =>
        Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(path)));
}
