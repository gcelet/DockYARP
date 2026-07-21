namespace DockYarp.Docker.Tests;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using DockYarp.Docker.Discovery;
using DockYarp.Docker.Models;

/// <summary>An in-memory <see cref="IContainerSource"/> for deterministic discovery tests.</summary>
internal sealed class FakeContainerSource : IContainerSource
{
    private readonly Channel<ContainerLifecycleEvent> events = Channel.CreateUnbounded<ContainerLifecycleEvent>();
    private volatile IReadOnlyList<ContainerInfo> containers = [];
    private int watchFailuresRemaining;
    private int listCallCount;

    public int ListCallCount => Volatile.Read(ref listCallCount);

    public void SetContainers(params ContainerInfo[] value) => containers = value;

    public void FailNextWatch(int times) => Volatile.Write(ref watchFailuresRemaining, times);

    public void RaiseEvent(ContainerLifecycleEvent lifecycleEvent) => events.Writer.TryWrite(lifecycleEvent);

    public Task<IReadOnlyList<ContainerInfo>> ListRunningContainersAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref listCallCount);
        return Task.FromResult(containers);
    }

    public async IAsyncEnumerable<ContainerLifecycleEvent> WatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (Interlocked.Decrement(ref watchFailuresRemaining) >= 0)
        {
            throw new InvalidOperationException("Simulated Docker watch failure.");
        }

        await foreach (ContainerLifecycleEvent lifecycleEvent in
            events.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return lifecycleEvent;
        }
    }
}
