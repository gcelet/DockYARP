namespace DockYarp.Core.Models;

using System;
using System.Collections.Immutable;

/// <summary>A backend service made of one or more interchangeable endpoints.</summary>
/// <remarks>Execution of the load-balancing policy and health probes belongs to the proxy layer;
/// this type only models the intended configuration.</remarks>
public sealed record Cluster
{
    /// <summary>Gets the stable cluster identity referenced by routes.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the destinations that make up the cluster.</summary>
    public required ImmutableArray<ClusterEndpoint> Endpoints { get; init; }

    /// <summary>Gets the policy used to pick an endpoint per request.</summary>
    public LoadBalancingPolicy LoadBalancingPolicy { get; init; } = LoadBalancingPolicy.RoundRobin;

    /// <summary>Gets the client-affinity ("sticky session") policy.</summary>
    /// <remarks><see cref="Models.SessionAffinityPolicy.ClientIpHash"/> needs no Data Protection;
    /// <see cref="Models.SessionAffinityPolicy.Cookie"/> and <see cref="Models.SessionAffinityPolicy.CustomHeader"/>
    /// do, and degrade to no affinity (not a hard failure) when it isn't configured.</remarks>
    public SessionAffinityPolicy SessionAffinityPolicy { get; init; } = SessionAffinityPolicy.None;

    /// <summary>Gets the optional health-check configuration.</summary>
    public HealthCheckConfig? HealthCheck { get; init; }

    /// <summary>Gets the optional request timeout applied to the cluster's outgoing (proxied) requests.</summary>
    public TimeSpan? RequestTimeout { get; init; }

    /// <summary>Gets a value indicating whether the backend is contacted over HTTP/2 only (a gRPC backend).</summary>
    public bool Http2Only { get; init; }

    /// <summary>Gets the maximum concurrent connections opened to the backend, if set (else YARP's default pooling).</summary>
    public int? MaxConnectionsPerServer { get; init; }
}
