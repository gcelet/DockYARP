namespace DockYarp.AdminApi;

using System;

using DockYarp.Core.Interfaces;
using DockYarp.Core.Models;

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
