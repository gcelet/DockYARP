namespace DockYarp.Security;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/// <summary>Loads Apache htpasswd files (per host and per path) for file-based Basic Auth.</summary>
/// <remarks>
/// A file named <c>&lt;host&gt;</c> protects the whole vhost; <c>&lt;host&gt;_&lt;sha1hex(path)&gt;</c> protects a
/// specific path (nginx-proxy's naming scheme). The parsed files are held in a snapshot that <see cref="Reload"/>
/// swaps atomically, so <see cref="Find"/> reads it lock-free.
/// </remarks>
public sealed class HtpasswdStore
{
    private readonly string? directory;
    private volatile IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> files;

    /// <summary>Loads the htpasswd files from the configured directory, if any.</summary>
    /// <param name="options">Security options carrying the htpasswd directory.</param>
    public HtpasswdStore(SecurityHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        directory = options.HtpasswdDirectory;
        files = Load(directory);
    }

    /// <summary>Finds the htpasswd entries governing a route, preferring a path-scoped file over the host file.</summary>
    /// <param name="host">The route host.</param>
    /// <param name="pathPrefix">The route path prefix, if any.</param>
    /// <returns>The user→hash entries protecting the route, or <see langword="null"/> when none apply.</returns>
    public IReadOnlyDictionary<string, string>? Find(string host, string? pathPrefix)
    {
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> snapshot = files;
        if (pathPrefix is { Length: > 0 } path
            && !string.Equals(path, "/", StringComparison.Ordinal)
            && snapshot.TryGetValue($"{host}_{PathHash(path)}", out IReadOnlyDictionary<string, string>? scoped))
        {
            return scoped;
        }

        return snapshot.GetValueOrDefault(host);
    }

    /// <summary>Re-reads the htpasswd directory and atomically swaps the in-memory snapshot.</summary>
    public void Reload() => files = Load(directory);

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Load(string? directory)
    {
        Dictionary<string, IReadOnlyDictionary<string, string>> result = new(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return result;
        }

        try
        {
            foreach (string file in Directory.EnumerateFiles(directory))
            {
                if (TryLoadFile(file, out IReadOnlyDictionary<string, string>? entries))
                {
                    result[Path.GetFileName(file)] = entries;
                }
            }
        }
        catch (IOException)
        {
            // The directory vanished mid-reload; return whatever was gathered.
        }

        return result;
    }

    private static bool TryLoadFile(string file, [NotNullWhen(true)] out IReadOnlyDictionary<string, string>? entries)
    {
        Dictionary<string, string> parsed = new(StringComparer.Ordinal);
        try
        {
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
                    parsed[trimmed[..separator]] = trimmed[(separator + 1)..];
                }
            }
        }
        catch (IOException)
        {
            // The file is mid-write (sharing violation / truncated); skip it this cycle.
            entries = null;
            return false;
        }

        entries = parsed;
        return true;
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
