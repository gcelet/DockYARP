namespace DockYarp.Docker.Models;

/// <summary>The kind of container lifecycle change reported by the Docker daemon.</summary>
public enum ContainerEventKind
{
    /// <summary>A container started.</summary>
    Started,

    /// <summary>A container stopped.</summary>
    Stopped,

    /// <summary>A container died.</summary>
    Died,

    /// <summary>A container's configuration was updated.</summary>
    Updated,
}
