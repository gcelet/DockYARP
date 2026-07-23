namespace DockYarp.App.StaticConfig;

/// <summary>Options for the static (file-based) configuration source.</summary>
public sealed class StaticConfigOptions
{
    /// <summary>Gets or sets the path to the JSON static configuration file; <see langword="null"/> disables it.</summary>
    public string? Path { get; set; }
}
