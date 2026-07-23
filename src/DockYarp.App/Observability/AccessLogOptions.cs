namespace DockYarp.App.Observability;

/// <summary>Options for per-request access logging.</summary>
public sealed class AccessLogOptions
{
    /// <summary>Gets or sets a value indicating whether an access-log entry is emitted per request.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the request path prefixes excluded from access logging (infrastructure endpoints).</summary>
    public string[] ExcludedPathPrefixes { get; set; } = ["/metrics", "/api"];
}
