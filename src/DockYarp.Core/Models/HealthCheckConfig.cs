namespace DockYarp.Core.Models;

using System;

/// <summary>Health-check configuration for a cluster's endpoints.</summary>
public sealed record HealthCheckConfig
{
    /// <summary>Gets a value indicating whether active probing is enabled.</summary>
    public bool ActiveEnabled { get; init; }

    /// <summary>Gets the relative path probed when active health checks are enabled.</summary>
    public string? Path { get; init; }

    /// <summary>Gets the interval between active probes.</summary>
    public TimeSpan Interval { get; init; }
}
