namespace DockYarp.Core.Configuration;

using System;
using System.Collections.Generic;

/// <summary>Structured per-host and global overrides layered onto the generated routes (the YARP-native
/// analog of nginx-proxy's <c>vhost.d</c>).</summary>
public sealed record ConfigOverrides
{
    /// <summary>An empty override set (no per-host or default overrides).</summary>
    public static ConfigOverrides Empty { get; } = new();

    /// <summary>Gets the per-host response headers, keyed by host (case-insensitive).</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ResponseHeadersByHost { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the global (<c>default</c>) response headers applied to hosts without a specific override.</summary>
    public IReadOnlyDictionary<string, string>? DefaultResponseHeaders { get; init; }
}
