namespace DockYarp.Docker.Discovery;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using DockYarp.Docker.Models;

/// <summary>Provides the current containers and a stream of container lifecycle events.</summary>
/// <remarks>Abstracts the Docker daemon so discovery logic can be unit tested with a fake source.</remarks>
public interface IContainerSource
{
    /// <summary>Lists the currently running containers.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The running containers.</returns>
    Task<IReadOnlyList<ContainerInfo>> ListRunningContainersAsync(CancellationToken cancellationToken);

    /// <summary>Streams container lifecycle events until cancelled or the connection drops.</summary>
    /// <param name="cancellationToken">Token to stop watching.</param>
    /// <returns>An asynchronous stream of lifecycle events.</returns>
    IAsyncEnumerable<ContainerLifecycleEvent> WatchAsync(CancellationToken cancellationToken);
}
