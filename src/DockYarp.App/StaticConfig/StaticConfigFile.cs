namespace DockYarp.App.StaticConfig;

/// <summary>JSON shape of the static configuration file.</summary>
internal sealed record StaticConfigFile
{
    /// <summary>Gets the declared clusters.</summary>
    public ClusterEntry[]? Clusters { get; init; }

    /// <summary>Gets the declared routes.</summary>
    public RouteEntry[]? Routes { get; init; }

    /// <summary>A cluster declared in the static configuration file.</summary>
    internal sealed record ClusterEntry
    {
        /// <summary>Gets the cluster id.</summary>
        public string? Id { get; init; }

        /// <summary>Gets the absolute backend addresses.</summary>
        public string[]? Addresses { get; init; }

        /// <summary>Gets the load-balancing policy (<c>round-robin</c> or <c>least-requests</c>).</summary>
        public string? LoadBalancing { get; init; }
    }

    /// <summary>A route declared in the static configuration file.</summary>
    internal sealed record RouteEntry
    {
        /// <summary>Gets the host pattern.</summary>
        public string? Host { get; init; }

        /// <summary>Gets the optional path prefix.</summary>
        public string? Path { get; init; }

        /// <summary>Gets the target cluster id.</summary>
        public string? Cluster { get; init; }

        /// <summary>Gets the route priority.</summary>
        public int Priority { get; init; }
    }
}
