namespace DockYarp.Core.Configuration;

/// <summary>A static configuration provider that contributes nothing (the neutral default).</summary>
public sealed class EmptyStaticConfigProvider : IStaticConfigProvider
{
    /// <inheritdoc />
    public ConfigContribution GetContribution() => new(ConfigSource.Static, [], []);
}
