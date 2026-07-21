namespace DockYarp.Core.Configuration;

/// <summary>Origin of a configuration contribution, used to resolve precedence.</summary>
public enum ConfigSource
{
    /// <summary>Routes/clusters from dynamic discovery (for example Docker).</summary>
    Dynamic,

    /// <summary>Routes/clusters from static configuration; takes precedence over <see cref="Dynamic"/>.</summary>
    Static,
}
