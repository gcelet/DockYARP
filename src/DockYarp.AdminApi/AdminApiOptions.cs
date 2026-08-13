namespace DockYarp.AdminApi;

/// <summary>Options for the admin API.</summary>
public sealed class AdminApiOptions
{
    /// <summary>Gets or sets the API key required in the <c>X-Api-Key</c> header. When null/empty the admin API is closed.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets the dedicated host the admin endpoints (<c>/api/*</c> and <c>/metrics</c>) are scoped to.</summary>
    /// <remarks>When set, those paths respond only on this host; on any other host they fall through to proxying (so a backend's <c>/api/*</c> is not shadowed). When null/empty, the admin endpoints answer on all hosts.</remarks>
    public string? Host { get; set; }
}
