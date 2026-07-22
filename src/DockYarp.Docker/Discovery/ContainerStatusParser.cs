namespace DockYarp.Docker.Discovery;

using System;

using DockYarp.Docker.Models;

/// <summary>Parses Docker status strings and event actions into DockYarp's discovery model.</summary>
/// <remarks>Pure and side-effect free so it can be unit tested without a Docker daemon.</remarks>
public static class ContainerStatusParser
{
    /// <summary>Reads the health state embedded in a container's status string (e.g. <c>Up 2 minutes (healthy)</c>).</summary>
    /// <param name="status">The Docker container status text.</param>
    /// <returns>The parsed health, or <see cref="ContainerHealth.None"/> when no health suffix is present.</returns>
    public static ContainerHealth ParseHealth(string? status)
    {
        if (string.IsNullOrEmpty(status))
        {
            return ContainerHealth.None;
        }

        if (status.Contains("(unhealthy)", StringComparison.OrdinalIgnoreCase))
        {
            return ContainerHealth.Unhealthy;
        }

        if (status.Contains("(health: starting)", StringComparison.OrdinalIgnoreCase))
        {
            return ContainerHealth.Starting;
        }

        return status.Contains("(healthy)", StringComparison.OrdinalIgnoreCase)
            ? ContainerHealth.Healthy
            : ContainerHealth.None;
    }

    /// <summary>Maps a Docker container event action to a lifecycle event kind.</summary>
    /// <param name="action">The Docker event action (e.g. <c>start</c>, <c>health_status: healthy</c>).</param>
    /// <returns>The lifecycle kind, or <see langword="null"/> when the action is not relevant.</returns>
    public static ContainerEventKind? MapAction(string? action)
    {
        if (string.IsNullOrEmpty(action))
        {
            return null;
        }

        // Health transitions ("health_status: healthy|unhealthy") reconcile so the endpoint set follows health.
        if (action.StartsWith("health_status", StringComparison.Ordinal))
        {
            return ContainerEventKind.Updated;
        }

        return action switch
        {
            "start" => ContainerEventKind.Started,
            "stop" => ContainerEventKind.Stopped,
            "die" => ContainerEventKind.Died,
            "update" => ContainerEventKind.Updated,
            _ => null,
        };
    }
}
