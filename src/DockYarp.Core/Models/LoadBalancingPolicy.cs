namespace DockYarp.Core.Models;

/// <summary>Load-balancing policy applied when a cluster has more than one endpoint.</summary>
public enum LoadBalancingPolicy
{
    /// <summary>Distribute requests evenly in rotation.</summary>
    RoundRobin,

    /// <summary>Prefer the endpoint with the fewest active requests.</summary>
    LeastRequests,
}
