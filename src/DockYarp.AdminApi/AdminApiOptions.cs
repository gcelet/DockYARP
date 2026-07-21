namespace DockYarp.AdminApi;

/// <summary>Options for the admin API.</summary>
public sealed class AdminApiOptions
{
    /// <summary>Gets or sets the API key required in the <c>X-Api-Key</c> header. When null/empty the admin API is closed.</summary>
    public string? ApiKey { get; set; }
}
