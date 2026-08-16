namespace DockYarp.AdminApi;

using System;
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

    /// <summary>Resolves the overall status and discovery status strings from the current discovery health.</summary>
    /// <param name="discovery">The discovery health to resolve.</param>
    /// <returns>The overall status (<c>Healthy</c>/<c>Degraded</c>) and the discovery status
    /// (<c>connected</c>/<c>disconnected</c>/<c>disabled</c>).</returns>
    public static (string Status, string Discovery) ResolveHealth(IDiscoveryHealth discovery)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        string discoveryStatus = (discovery.Enabled, discovery.Connected) switch
        {
            (false, _) => "disabled",
            (true, true) => "connected",
            (true, false) => "disconnected",
        };
        string status = discovery.Enabled && !discovery.Connected ? "Degraded" : "Healthy";
        return (status, discoveryStatus);
    }

    /// <summary>Maps a matched route and its target cluster to the resolved-configuration view.</summary>
    /// <param name="route">The matched route.</param>
    /// <param name="cluster">The target cluster, or <see langword="null"/> when it is missing.</param>
    /// <returns>The resolved-configuration view.</returns>
    public static AdminApiModels.ResolveView Resolve(RouteRule route, Cluster? cluster)
    {
        ArgumentNullException.ThrowIfNull(route);
        return new AdminApiModels.ResolveView(
            ToRoute(route),
            ToTransforms(route.Transforms),
            new AdminApiModels.SecurityView
            {
                InternalOnly = route.InternalOnly,
                ClientCertificate = route.ClientCertificate.ToString(),
                MaxRequestBodySize = route.MaxRequestBodySize,
            },
            cluster is null ? null : ToCluster(cluster));
    }

    private static AdminApiModels.TransformsView? ToTransforms(RouteTransforms? transforms) =>
        transforms is null
            ? null
            : new AdminApiModels.TransformsView(transforms.PathRemovePrefix, transforms.PathAddPrefix, transforms.ResponseHeaders);

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
