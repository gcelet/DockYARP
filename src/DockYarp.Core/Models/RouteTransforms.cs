namespace DockYarp.Core.Models;

/// <summary>Minimal request-transform placeholder for a route.</summary>
/// <remarks>Kept intentionally small; richer transforms are introduced with the YARP integration.</remarks>
public sealed record RouteTransforms
{
    /// <summary>Gets the optional path prefix stripped before forwarding.</summary>
    public string? PathRemovePrefix { get; init; }
}
