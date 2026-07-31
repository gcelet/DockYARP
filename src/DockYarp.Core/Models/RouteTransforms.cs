namespace DockYarp.Core.Models;

using System.Collections.Generic;

/// <summary>Transforms applied to a route (path rewrites and response headers).</summary>
/// <remarks>Kept intentionally small; richer transforms are introduced as needed.</remarks>
public sealed record RouteTransforms
{
    /// <summary>Gets the optional path prefix stripped before forwarding.</summary>
    public string? PathRemovePrefix { get; init; }

    /// <summary>Gets the optional path prefix prepended after stripping (the <c>VIRTUAL_DEST</c> destination).</summary>
    public string? PathAddPrefix { get; init; }

    /// <summary>Gets response headers to set on the proxied response (from per-host / global overrides).</summary>
    public IReadOnlyDictionary<string, string>? ResponseHeaders { get; init; }
}
