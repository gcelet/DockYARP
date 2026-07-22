namespace DockYarp.AdminApi;

/// <summary>Reports Docker discovery health to the admin API.</summary>
/// <remarks>Implemented by the host, keeping AdminApi decoupled from the Docker module.</remarks>
public interface IDiscoveryHealth
{
    /// <summary>Gets a value indicating whether Docker discovery is enabled.</summary>
    bool Enabled { get; }

    /// <summary>Gets a value indicating whether discovery is currently connected (meaningful when enabled).</summary>
    bool Connected { get; }
}
