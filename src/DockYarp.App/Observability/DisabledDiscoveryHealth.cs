namespace DockYarp.App.Observability;

using DockYarp.AdminApi;

/// <summary>Reports discovery as disabled (used when Docker discovery is not wired).</summary>
public sealed class DisabledDiscoveryHealth : IDiscoveryHealth
{
    /// <inheritdoc />
    public bool Enabled => false;

    /// <inheritdoc />
    public bool Connected => false;
}
