namespace DockYarp.Docker.Discovery;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using DockYarp.Core.Configuration;
using DockYarp.Core.Interfaces;
using DockYarp.Core.Models;
using DockYarp.Docker.Mapping;
using DockYarp.Docker.Models;

using Microsoft.Extensions.Logging;

/// <summary>Reconciles the current set of containers into the routing store.</summary>
/// <remarks>
/// Lists running containers, maps them into a dynamic contribution, merges it (resolving precedence and
/// reporting diagnostics), and publishes the result. Running it on startup and after each event/reconnect
/// converges the active configuration to reality with a single code path.
/// </remarks>
/// <param name="source">The container source.</param>
/// <param name="store">The route configuration store to publish into.</param>
/// <param name="staticConfig">The static configuration source, merged with (and winning over) discovery.</param>
/// <param name="logger">Logger for reconciliation diagnostics.</param>
public sealed class DiscoveryReconciler(
    IContainerSource source,
    IRouteConfigStore store,
    IStaticConfigProvider staticConfig,
    ILogger<DiscoveryReconciler> logger)
{
    /// <summary>Performs one reconciliation pass.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the store has been updated.</returns>
    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ContainerInfo> containers =
            await source.ListRunningContainersAsync(cancellationToken).ConfigureAwait(false);

        ContainerMapResult mapResult = ContainerMapper.Map(containers);
        foreach (string warning in mapResult.Warnings)
        {
            DiscoveryLog.ContainerSkipped(logger, warning);
        }

        MergeResult merge = RouteConfigMerger.Merge([staticConfig.GetContribution(), mapResult.Contribution]);
        foreach (MergeDiagnostic diagnostic in merge.Diagnostics)
        {
            DiscoveryLog.MergeDiagnostic(logger, diagnostic.Code, diagnostic.Message);
        }

        // Layer per-host / global overrides (e.g. response headers) onto the merged routes.
        ImmutableArray<RouteRule> routes = RouteOverrideApplier.Apply(merge.Routes, staticConfig.GetOverrides());
        store.Apply(routes, merge.Clusters);
        DiscoveryLog.Reconciled(logger, routes.Length, merge.Clusters.Length, containers.Count);
    }
}
