namespace DockYarp.Docker.Discovery;

/// <summary>Tracks whether Docker discovery is currently connected to the daemon.</summary>
/// <remarks>Updated by the discovery background service; read by observability (health) consumers.</remarks>
public sealed class DiscoveryHealthState
{
    private volatile bool connected;

    /// <summary>Gets a value indicating whether discovery is currently connected.</summary>
    public bool Connected => connected;

    /// <summary>Marks discovery as connected (a reconcile/watch cycle succeeded).</summary>
    public void MarkConnected() => connected = true;

    /// <summary>Marks discovery as disconnected (a failure or stream end occurred).</summary>
    public void MarkDisconnected() => connected = false;
}
