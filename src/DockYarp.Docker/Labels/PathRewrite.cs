namespace DockYarp.Docker.Labels;

/// <summary>Resolves a <c>VIRTUAL_DEST</c>/<c>VIRTUAL_PATH</c> pair into path-rewrite transform prefixes.</summary>
internal static class PathRewrite
{
    /// <summary>Resolves the strip and prepend prefixes for a destination rewrite.</summary>
    /// <param name="dest">The <c>VIRTUAL_DEST</c> value, if any.</param>
    /// <param name="path">The matched <c>VIRTUAL_PATH</c>, if any.</param>
    /// <returns>The prefix to remove and the prefix to prepend; either may be <see langword="null"/>.</returns>
    /// <remarks>
    /// Parity with nginx-proxy: a non-empty <c>VIRTUAL_DEST</c> strips the matched <c>VIRTUAL_PATH</c>, and a
    /// non-root destination is then prepended, so <c>/api</c> with dest <c>/v2</c> rewrites to <c>/v2</c>. A root
    /// (<c>/</c>) destination keeps the pure strip; an absent destination forwards the original path.
    /// </remarks>
    public static (string? Remove, string? Add) Resolve(string? dest, string? path)
    {
        // A regex VIRTUAL_PATH (~-prefixed) has no fixed prefix to strip, so VIRTUAL_DEST does not apply
        // (nginx-proxy forbids the combination); ignore the destination.
        if (string.IsNullOrEmpty(dest) || string.IsNullOrEmpty(path) || path[0] == '~')
        {
            return (null, null);
        }

        string trimmed = dest.Trim('/');
        return (path, trimmed.Length > 0 ? $"/{trimmed}" : null);
    }
}
