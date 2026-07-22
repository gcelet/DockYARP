namespace DockYarp.Docker.Discovery;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using global::Docker.DotNet;
using global::Docker.DotNet.Models;

using DockYarp.Docker.Models;

/// <summary><see cref="IContainerSource"/> backed by the Docker daemon via Docker.DotNet.</summary>
public sealed class DockerContainerSource : IContainerSource, IDisposable
{
    private readonly IDockerClient client;
    private readonly string? preferredNetwork;

    /// <summary>Initializes the source, creating a Docker client from the options.</summary>
    /// <param name="options">Discovery options (endpoint, preferred network).</param>
    public DockerContainerSource(DockerDiscoveryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        client = CreateClient(options.DockerEndpoint);
        preferredNetwork = options.PreferredNetwork;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContainerInfo>> ListRunningContainersAsync(CancellationToken cancellationToken)
    {
        IList<ContainerListResponse> responses = await client.Containers
            .ListContainersAsync(new ContainersListParameters { All = false }, cancellationToken)
            .ConfigureAwait(false);

        List<ContainerInfo> result = new(responses.Count);
        foreach (ContainerListResponse response in responses)
        {
            result.Add(ToContainerInfo(response));
        }

        return result;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ContainerLifecycleEvent> WatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Channel<ContainerLifecycleEvent> channel = Channel.CreateUnbounded<ContainerLifecycleEvent>();
        Progress<Message> progress = new(message =>
        {
            if (TryMapEvent(message, out ContainerLifecycleEvent? mapped))
            {
                channel.Writer.TryWrite(mapped);
            }
        });

        Task monitor = MonitorAsync(progress, channel.Writer, cancellationToken);
        await foreach (ContainerLifecycleEvent lifecycleEvent in
            channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return lifecycleEvent;
        }

        await monitor.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        client.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task MonitorAsync(
        IProgress<Message> progress,
        ChannelWriter<ContainerLifecycleEvent> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.System
                .MonitorEventsAsync(new ContainerEventsParameters(), progress, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private static IDockerClient CreateClient(string? endpoint)
    {
        DockerClientConfiguration configuration = endpoint is { Length: > 0 }
            ? new DockerClientConfiguration(new Uri(endpoint))
            : new DockerClientConfiguration();
        return configuration.CreateClient();
    }

    private ContainerInfo ToContainerInfo(ContainerListResponse response) =>
        new()
        {
            Id = response.ID,
            Name = ResolveName(response.Names),
            Address = ResolveAddress(response),
            Labels = CopyLabels(response.Labels),
            ExposedPorts = ResolvePorts(response.Ports),
            Health = ContainerStatusParser.ParseHealth(response.Status),
        };

    private static IReadOnlyDictionary<string, string> CopyLabels(IDictionary<string, string>? labels) =>
        labels is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(labels, StringComparer.Ordinal);

    private static string ResolveName(IList<string>? names) =>
        names is { Count: > 0 } ? names[0].TrimStart('/') : string.Empty;

    private string ResolveAddress(ContainerListResponse response)
    {
        // Select by network (preferred network, ingress skipped, deterministic); fall back to the
        // container name (resolvable on a shared network) when no usable network address is found.
        Dictionary<string, string?> networks = response.NetworkSettings?.Networks is { } map
            ? map.ToDictionary(pair => pair.Key, pair => pair.Value?.IPAddress, StringComparer.Ordinal)
            : new Dictionary<string, string?>(StringComparer.Ordinal);
        string? ip = NetworkAddressSelector.Select(networks, preferredNetwork);
        return string.IsNullOrEmpty(ip) ? ResolveName(response.Names) : ip;
    }

    private static ImmutableArray<int> ResolvePorts(IList<Port>? ports)
    {
        if (ports is null || ports.Count == 0)
        {
            return [];
        }

        HashSet<int> distinct = [];
        foreach (Port port in ports)
        {
            distinct.Add(port.PrivatePort);
        }

        return [.. distinct];
    }

    private static bool TryMapEvent(Message message, [MaybeNullWhen(false)] out ContainerLifecycleEvent lifecycleEvent)
    {
        lifecycleEvent = null;
        if (!string.Equals(message.Type, "container", StringComparison.Ordinal))
        {
            return false;
        }

        ContainerEventKind? kind = ContainerStatusParser.MapAction(message.Action);
        if (kind is null)
        {
            return false;
        }

        lifecycleEvent = new ContainerLifecycleEvent(kind.Value, message.Actor?.ID ?? message.ID);
        return true;
    }
}
