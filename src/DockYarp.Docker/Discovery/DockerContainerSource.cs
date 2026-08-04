namespace DockYarp.Docker.Discovery;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using DockYarp.Docker.Models;

using global::Docker.DotNet;
using global::Docker.DotNet.Models;

/// <summary><see cref="IContainerSource"/> backed by the Docker daemon via Docker.DotNet.</summary>
public sealed class DockerContainerSource : IContainerSource, IDisposable
{
    private static readonly IReadOnlyDictionary<string, string> EmptyEnv =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly IDockerClient client;
    private readonly string? preferredNetwork;
    private readonly string? hostAddress;
    private readonly IReadOnlyCollection<string> proxyNetworks;
    private readonly IDictionary<string, IDictionary<string, bool>>? containerFilters;

    /// <summary>Initializes the source, creating a Docker client from the options.</summary>
    /// <param name="options">Discovery options (endpoint, preferred/proxy networks, host address, container filters).</param>
    public DockerContainerSource(DockerDiscoveryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        client = CreateClient(options);
        preferredNetwork = options.PreferredNetwork;
        hostAddress = options.HostAddress;
        proxyNetworks = [.. options.ProxyNetworks];
        containerFilters = DockerFilters.Build(options.ContainerFilters);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContainerInfo>> ListRunningContainersAsync(CancellationToken cancellationToken)
    {
        // Scope discovery to the configured containers; the listing is authoritative (every event reconciles
        // against it), so the event stream itself stays unfiltered. See the change's design.md.
        IList<ContainerListResponse> responses = await client.Containers
            .ListContainersAsync(new ContainersListParameters { All = false, Filters = containerFilters }, cancellationToken)
            .ConfigureAwait(false);

        List<ContainerInfo> result = new(responses.Count);
        foreach (ContainerListResponse response in responses)
        {
            // The list response omits env vars; inspect the container to read Config.Env (nginx-proxy's
            // canonical config channel). See the change's design.md.
            IReadOnlyDictionary<string, string> env =
                await InspectEnvAsync(response.ID, cancellationToken).ConfigureAwait(false);
            result.Add(ToContainerInfo(response, env));
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<string, string>> InspectEnvAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            ContainerInspectResponse inspect =
                await client.Containers.InspectContainerAsync(id, cancellationToken).ConfigureAwait(false);
            return ContainerEnvParser.Parse(inspect.Config?.Env);
        }
        catch (DockerApiException)
        {
            // The container vanished between listing and inspecting, or the daemon rejected the call; fall back
            // to labels only for this container (the next reconcile corrects it).
            return EmptyEnv;
        }
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

    private static IDockerClient CreateClient(DockerDiscoveryOptions options)
    {
        Uri? endpoint = options.DockerEndpoint is { Length: > 0 } uri ? new Uri(uri) : null;
        Credentials? credentials = BuildTlsCredentials(options, endpoint);
        DockerClientConfiguration configuration = (endpoint, credentials) switch
        {
            (not null, not null) => new DockerClientConfiguration(endpoint, credentials),
            (not null, null) => new DockerClientConfiguration(endpoint),
            _ => new DockerClientConfiguration(),
        };
        return configuration.CreateClient();
    }

    // Reads the TLS material from CertPath (real IO) and builds credentials for a tcp:// endpoint; a socket
    // endpoint or a missing CertPath yields null (unchanged connection). The construction itself is unit-tested
    // via DockerTlsCredentials.
    private static Credentials? BuildTlsCredentials(DockerDiscoveryOptions options, Uri? endpoint)
    {
        if (endpoint is null || options.CertPath is not { Length: > 0 } directory)
        {
            return null;
        }

        return DockerTlsCredentials.Create(
            endpoint,
            options.TlsVerify ? DaemonTlsVerification.VerifyAgainstCa : DaemonTlsVerification.AcceptAny,
            ReadPemOrNull(directory, "ca.pem"),
            ReadPemOrNull(directory, "cert.pem"),
            ReadPemOrNull(directory, "key.pem"));
    }

    private static string? ReadPemOrNull(string directory, string fileName)
    {
        string path = Path.Combine(directory, fileName);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private ContainerInfo ToContainerInfo(ContainerListResponse response, IReadOnlyDictionary<string, string> env)
    {
        Dictionary<string, string?> networks = BuildNetworks(response);
        bool hostMode = BackendAddressResolver.IsHostNetwork(networks);
        return new()
        {
            Id = response.ID,
            Name = ResolveName(response.Names),
            Address = ResolveAddress(response, networks, hostMode),
            IsHostNetwork = hostMode,
            Labels = CopyLabels(response.Labels),
            Env = env,
            ExposedPorts = ResolvePorts(response.Ports),
            Health = ContainerStatusParser.ParseHealth(response.Status),
        };
    }

    private static IReadOnlyDictionary<string, string> CopyLabels(IDictionary<string, string>? labels) =>
        labels is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(labels, StringComparer.Ordinal);

    private static string ResolveName(IList<string>? names) =>
        names is { Count: > 0 } ? names[0].TrimStart('/') : string.Empty;

    private static Dictionary<string, string?> BuildNetworks(ContainerListResponse response) =>
        response.NetworkSettings?.Networks is { } map
            ? map.ToDictionary(pair => pair.Key, pair => pair.Value?.IPAddress, StringComparer.Ordinal)
            : new Dictionary<string, string?>(StringComparer.Ordinal);

    private string ResolveAddress(ContainerListResponse response, Dictionary<string, string?> networks, bool hostMode)
    {
        // Host mode has no container IP; otherwise select by network (preferred, ingress skipped, reachable-only,
        // deterministic). The resolver falls back to the host address, empty (skip), or the container name.
        string? ip = hostMode ? null : NetworkAddressSelector.Select(networks, preferredNetwork, proxyNetworks);
        return BackendAddressResolver.Resolve(
            networks, hostAddress, ip, ResolveName(response.Names), proxyNetworks);
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
