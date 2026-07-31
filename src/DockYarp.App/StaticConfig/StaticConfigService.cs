namespace DockYarp.App.StaticConfig;

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using DockYarp.Core.Configuration;
using DockYarp.Core.Interfaces;
using DockYarp.Core.Models;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Applies the static configuration to the routing store at startup (used when Docker discovery is off).</summary>
/// <remarks>When discovery is enabled, the reconciler merges the static contribution instead, so this is not registered.</remarks>
/// <param name="provider">The static configuration provider.</param>
/// <param name="store">The routing store to publish into.</param>
/// <param name="logger">Logger for merge diagnostics.</param>
public sealed class StaticConfigService(
    IStaticConfigProvider provider,
    IRouteConfigStore store,
    ILogger<StaticConfigService> logger) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        MergeResult merge = RouteConfigMerger.Merge([provider.GetContribution()]);
        foreach (MergeDiagnostic diagnostic in merge.Diagnostics)
        {
            StaticConfigLog.MergeDiagnostic(logger, diagnostic.Code, diagnostic.Message);
        }

        ImmutableArray<RouteRule> routes = RouteOverrideApplier.Apply(merge.Routes, provider.GetOverrides());
        store.Apply(routes, merge.Clusters);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
