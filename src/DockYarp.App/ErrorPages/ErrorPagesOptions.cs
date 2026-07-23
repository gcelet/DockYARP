namespace DockYarp.App.ErrorPages;

/// <summary>Options for custom error pages.</summary>
public sealed class ErrorPagesOptions
{
    /// <summary>Gets or sets the directory holding <c>{statusCode}.html</c> pages; <see langword="null"/> disables them.</summary>
    public string? Directory { get; set; }
}
