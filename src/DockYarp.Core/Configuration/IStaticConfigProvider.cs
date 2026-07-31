namespace DockYarp.Core.Configuration;

/// <summary>Supplies the static configuration contribution (routes and clusters declared out of band).</summary>
public interface IStaticConfigProvider
{
    /// <summary>Gets the static configuration contribution (empty when none is configured).</summary>
    /// <returns>A <see cref="ConfigSource.Static"/> contribution.</returns>
    ConfigContribution GetContribution();

    /// <summary>Gets the structured per-host / global overrides layered onto the merged routes.</summary>
    /// <returns>The configured overrides; <see cref="ConfigOverrides.Empty"/> when none are declared.</returns>
    ConfigOverrides GetOverrides() => ConfigOverrides.Empty;
}
