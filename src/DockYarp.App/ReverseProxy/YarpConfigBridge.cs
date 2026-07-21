namespace DockYarp.App.ReverseProxy;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using DockYarp.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Yarp.ReverseProxy.Configuration;

/// <summary>Keeps YARP's in-memory configuration in sync with the routing store.</summary>
/// <remarks>
/// Pushes the current snapshot on start and then updates YARP whenever the store raises
/// <see cref="IRouteConfigStore.Changed"/>. Unsubscribes on stop.
/// </remarks>
/// <param name="store">The routing store.</param>
/// <param name="provider">YARP's in-memory configuration provider.</param>
public sealed class YarpConfigBridge(IRouteConfigStore store, InMemoryConfigProvider provider)
    : IHostedService, IDisposable
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        store.Changed += OnStoreChanged;
        Publish();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        store.Changed -= OnStoreChanged;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        store.Changed -= OnStoreChanged;
        GC.SuppressFinalize(this);
    }

    private void OnStoreChanged(object? sender, EventArgs e) => Publish();

    private void Publish()
    {
        (IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters) =
            YarpConfigMapper.Map(store.Current);
        provider.Update(routes, clusters);
    }
}
