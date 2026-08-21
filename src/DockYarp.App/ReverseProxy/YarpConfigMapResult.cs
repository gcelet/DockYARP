namespace DockYarp.App.ReverseProxy;

using System.Collections.Generic;

using Yarp.ReverseProxy.Configuration;

/// <summary>The outcome of mapping a routing snapshot to YARP configuration.</summary>
public sealed record YarpConfigMapResult
{
    /// <summary>Gets the mapped YARP routes.</summary>
    public required IReadOnlyList<RouteConfig> Routes { get; init; }

    /// <summary>Gets the mapped YARP clusters.</summary>
    public required IReadOnlyList<ClusterConfig> Clusters { get; init; }

    /// <summary>Gets diagnostics for clusters whose requested affinity policy could not be applied
    /// (e.g. <c>Cookie</c>/<c>CustomHeader</c> without Data Protection configured).</summary>
    public required IReadOnlyList<string> Diagnostics { get; init; }
}
