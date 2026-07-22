namespace DockYarp.AdminApi;

using System.Collections.Generic;
using System.Linq;

using DockYarp.Core.Models;

/// <summary>Maps the routing snapshot to sanitized admin read models.</summary>
public static class AdminMapper
{
    /// <summary>Maps the snapshot's routes to sanitized views.</summary>
    /// <param name="snapshot">The routing snapshot.</param>
    /// <returns>The route views.</returns>
    public static IReadOnlyList<AdminApiModels.RouteView> Routes(RouteConfigSnapshot snapshot) =>
        [.. snapshot.Routes.Select(ToRoute)];

    /// <summary>Maps the snapshot's clusters to views.</summary>
    /// <param name="snapshot">The routing snapshot.</param>
    /// <returns>The cluster views.</returns>
    public static IReadOnlyList<AdminApiModels.ClusterView> Clusters(RouteConfigSnapshot snapshot) =>
        [.. snapshot.Clusters.Select(ToCluster)];

    private static AdminApiModels.RouteView ToRoute(RouteRule route) =>
        new()
        {
            Host = route.HostPattern,
            PathPrefix = route.PathPrefix,
            Priority = route.Priority,
            ClusterId = route.ClusterId,
            RequiresAuth = route.Auth is not null,
            Tls = route.Tls is null
                ? null
                : new AdminApiModels.TlsView { CertificateHost = route.Tls.CertificateHost, HttpsMethod = route.Tls.Method.ToString() },
        };

    private static AdminApiModels.ClusterView ToCluster(Cluster cluster) =>
        new(
            cluster.Id,
            cluster.LoadBalancingPolicy.ToString(),
            [.. cluster.Endpoints.Select(endpoint => new AdminApiModels.EndpointView(endpoint.Id, endpoint.Address))]);
}
