namespace DockYarp.AdminApi;

using System;
using System.Linq;
using System.Reflection;

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
    /// <summary>Maps <c>/api/{version,routes,clusters,certs,resolve,health}</c> behind the API-key filter.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="host">The dedicated admin host, or <see langword="null"/>/empty to respond on all hosts.</param>
    /// <returns>The same endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapAdminApi(this IEndpointRouteBuilder endpoints, string? host)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api").AddEndpointFilter<ApiKeyEndpointFilter>();

        // Scope the whole group to a dedicated host so /api/* on any other host falls through to proxying
        // (a backend's /api/health etc. is not shadowed by the admin API).
        if (host is { Length: > 0 } adminHost)
        {
            group.RequireHost(adminHost);
        }

        group.MapGet("/version", static () =>
            Results.Json(new AdminApiModels.VersionView(ResolveVersion()), AdminApiJsonContext.Default));
        group.MapGet("/routes", static (IRouteConfigStore store) =>
            Results.Json(AdminMapper.Routes(store.Current), AdminApiJsonContext.Default));
        group.MapGet("/clusters", static (IRouteConfigStore store) =>
            Results.Json(AdminMapper.Clusters(store.Current), AdminApiJsonContext.Default));
        group.MapGet("/certs", static (ICertificateInventory inventory) =>
            Results.Json(inventory.List(), AdminApiJsonContext.Default));

        // The DockYarp analog of nginx-proxy's DEBUG_ENDPOINT: resolve a host/path to its effective config,
        // using the same route matcher as the request pipeline.
        group.MapGet("/resolve", static (string? host, string? path, IRouteConfigStore store, RoutingOptions routing) =>
        {
            if (host is not { Length: > 0 })
            {
                return Results.BadRequest(new AdminApiModels.ErrorView("host is required"));
            }

            RouteConfigSnapshot snapshot = store.Current;
            RouteMatcher matcher = new(snapshot.Routes, routing.DefaultHost);
            if (!matcher.TryMatch(host, path is { Length: > 0 } ? path : "/", out RouteRule? route))
            {
                return Results.NotFound(new AdminApiModels.ResolveNotFoundView { Host = host, Path = path });
            }

            Cluster? cluster = snapshot.Clusters.FirstOrDefault(candidate => candidate.Id == route.ClusterId);
            return Results.Json(AdminMapper.Resolve(route, cluster), AdminApiJsonContext.Default);
        });
        group.MapGet("/health", static (IRouteConfigStore store, ICertificateInventory inventory, IDiscoveryHealth discovery) =>
        {
            RouteConfigSnapshot snapshot = store.Current;
            (string status, string discoveryStatus) = AdminMapper.ResolveHealth(discovery);
            return Results.Json(
                new AdminApiModels.HealthView(
                    status,
                    snapshot.Routes.Length,
                    snapshot.Clusters.Length,
                    inventory.List().Count,
                    discoveryStatus),
                AdminApiJsonContext.Default);
        });

        return endpoints;
    }

    // The entry assembly (DockYarp.App) carries the version stamped by the build; strip any trailing "+<sha>"
    // source-revision metadata the SDK appends to the informational version.
    private static string ResolveVersion()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(AdminEndpoints).Assembly;
        string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (informational is { Length: > 0 })
        {
            int plus = informational.IndexOf('+', StringComparison.Ordinal);
            return plus < 0 ? informational : informational[..plus];
        }

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }
}
