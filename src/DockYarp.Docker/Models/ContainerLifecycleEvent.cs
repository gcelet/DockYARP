namespace DockYarp.Docker.Models;

/// <summary>A normalized container lifecycle event.</summary>
/// <param name="Kind">The kind of change.</param>
/// <param name="ContainerId">The affected container id.</param>
public sealed record ContainerLifecycleEvent(ContainerEventKind Kind, string ContainerId);
