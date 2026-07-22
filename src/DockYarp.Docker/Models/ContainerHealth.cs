namespace DockYarp.Docker.Models;

/// <summary>The health state of a discovered container, as reported by Docker.</summary>
public enum ContainerHealth
{
    /// <summary>The container declares no health check.</summary>
    None,

    /// <summary>The health check is defined but has not yet reported healthy.</summary>
    Starting,

    /// <summary>The container is healthy.</summary>
    Healthy,

    /// <summary>The container's health check is failing.</summary>
    Unhealthy,
}
