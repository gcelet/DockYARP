namespace DockYarp.App.Observability;

/// <summary>Options for per-request access logging.</summary>
public sealed class AccessLogOptions
{
    /// <summary>Gets or sets a value indicating whether an access-log entry is emitted per request.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the request path prefixes excluded from access logging (infrastructure endpoints).</summary>
    public string[] ExcludedPathPrefixes { get; set; } = ["/metrics", "/api"];

    /// <summary>Gets or sets the ordered access-log field selection; <see langword="null"/>/empty emits the default fields.</summary>
    /// <remarks>
    /// Names are chosen from <see cref="AccessLogFields.Names"/> (case-insensitive); unknown names are ignored.
    /// When set, each entry contains exactly the listed fields, in order — the structured analog of nginx <c>LOG_FORMAT</c>.
    /// </remarks>
    public string[]? Fields { get; set; }
}
