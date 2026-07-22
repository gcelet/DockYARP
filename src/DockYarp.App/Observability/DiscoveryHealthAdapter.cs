namespace DockYarp.App.Observability;

using DockYarp.AdminApi;
using DockYarp.Docker.Discovery;

/// <summary>Reports Docker discovery health from the shared discovery state (used when discovery is enabled).</summary>
/// <param name="state">The shared discovery connection state.</param>
public sealed class DiscoveryHealthAdapter(DiscoveryHealthState state) : IDiscoveryHealth
{
    /// <inheritdoc />
    public bool Enabled => true;

    /// <inheritdoc />
    public bool Connected => state.Connected;
}
