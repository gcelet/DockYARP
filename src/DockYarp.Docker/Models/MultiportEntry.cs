namespace DockYarp.Docker.Models;

using DockYarp.Core.Models;

/// <summary>A single host/path → port mapping parsed from <c>VIRTUAL_HOST_MULTIPORTS</c>.</summary>
/// <param name="Host">The host the entry serves.</param>
/// <param name="Path">The request path (for example <c>/</c> or <c>/api</c>).</param>
/// <param name="Port">The target container port.</param>
/// <param name="Scheme">The backend scheme (from <c>proto</c>).</param>
/// <param name="Dest">The optional destination rewrite (<c>dest</c>); non-empty strips the path prefix.</param>
public sealed record MultiportEntry(string Host, string Path, int Port, BackendScheme Scheme, string? Dest);
