namespace DockYarp.Core.Models;

/// <summary>Load-balancing policy applied when a cluster has more than one endpoint.</summary>
public enum LoadBalancingPolicy
{
    /// <summary>Distribute requests evenly in rotation.</summary>
    RoundRobin,

    /// <summary>Prefer the endpoint with the fewest active requests.</summary>
    LeastRequests,

    /// <summary>Pick the less-loaded of two random endpoints (good balance at low cost).</summary>
    PowerOfTwoChoices,

    /// <summary>Pick a random endpoint.</summary>
    Random,

    /// <summary>Always pick the first endpoint by id (deterministic).</summary>
    FirstAlphabetical,
}
