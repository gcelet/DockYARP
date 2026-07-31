namespace DockYarp.AdminApi;

using System;
using System.Linq;

using DockYarp.Core.Configuration;
using DockYarp.Core.Interfaces;
using DockYarp.Core.Models;
using DockYarp.Core.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

/// <summary>Maps the read-only admin API endpoints.</summary>
public static class AdminEndpoints
{
    /// <summary>Maps <c>/api/{routes,clusters,certs,health}</c> behind the API-key filter.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapAdminApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api").AddEndpointFilter<ApiKeyEndpointFilter>();

        group.MapGet("/routes", static (IRouteConfigStore store) => Results.Json(AdminMapper.Routes(store.Current)));
        group.MapGet("/clusters", static (IRouteConfigStore store) => Results.Json(AdminMapper.Clusters(store.Current)));
        group.MapGet("/certs", static (ICertificateInventory inventory) => Results.Json(inventory.List()));

        // The DockYarp analog of nginx-proxy's DEBUG_ENDPOINT: resolve a host/path to its effective config,
        // using the same route matcher as the request pipeline.
        group.MapGet("/resolve", static (string? host, string? path, IRouteConfigStore store, RoutingOptions routing) =>
        {
            if (host is not { Length: > 0 })
            {
                return Results.BadRequest(new { error = "host is required" });
            }

            RouteConfigSnapshot snapshot = store.Current;
            RouteMatcher matcher = new(snapshot.Routes, routing.DefaultHost);
            if (!matcher.TryMatch(host, path is { Length: > 0 } ? path : "/", out RouteRule? route))
            {
                return Results.NotFound(new { host, path, matched = false });
            }

            Cluster? cluster = snapshot.Clusters.FirstOrDefault(candidate => candidate.Id == route.ClusterId);
            return Results.Json(AdminMapper.Resolve(route, cluster));
        });
        group.MapGet("/health", static (IRouteConfigStore store, ICertificateInventory inventory, IDiscoveryHealth discovery) =>
        {
            RouteConfigSnapshot snapshot = store.Current;
            string discoveryStatus = (discovery.Enabled, discovery.Connected) switch
            {
                (false, _) => "disabled",
                (true, true) => "connected",
                (true, false) => "disconnected",
            };
            string status = discovery.Enabled && !discovery.Connected ? "Degraded" : "Healthy";
            return Results.Json(new AdminApiModels.HealthView(
                status,
                snapshot.Routes.Length,
                snapshot.Clusters.Length,
                inventory.List().Count,
                discoveryStatus));
        });

        return endpoints;
    }
}
