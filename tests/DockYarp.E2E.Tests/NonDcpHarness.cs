namespace DockYarp.E2E.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Docker.DotNet;
using Docker.DotNet.Models;

/// <summary>Creates and tears down Docker containers/networks <em>outside</em> Aspire/DCP's management.</summary>
/// <remarks>
/// DCP attaches every AppHost-managed container to one network it controls, which blocks scenarios needing a
/// host-network container or a genuinely unreachable network. This harness talks to the Docker daemon directly
/// via <c>Docker.DotNet</c> (already a project dependency — used by <c>DockYarp.Docker</c> itself) so a test
/// can create resources DCP never sees. Not a second NUnit <c>[SetUpFixture]</c>: NUnit allows only one per
/// namespace (already taken by <see cref="AspireAppHostFixture"/>) — a consuming test class instead owns an
/// instance via its own <c>[OneTimeSetUp]</c>/<c>[OneTimeTearDown]</c>.
/// </remarks>
internal sealed class NonDcpHarness : IAsyncDisposable
{
    // No explicit endpoint: the same default resolution DockerContainerSource.cs falls back to when
    // Docker:DockerEndpoint is unset, which targets the same daemon Aspire/DCP itself resolves in this process.
    private readonly DockerClient client = new DockerClientBuilder().Build();
    private readonly List<string> containerIds = [];
    private readonly List<string> networkIds = [];

    /// <summary>Gets the underlying client, for smoke tests that need to call Docker.DotNet directly
    /// (e.g. <c>Networks.InspectNetworkAsync</c>) rather than through this harness's own wrapper methods.</summary>
    internal DockerClient Client => client;

    /// <summary>Pulls <paramref name="image"/> only if it isn't already present locally.</summary>
    /// <param name="image">The image reference, e.g. <c>traefik/whoami:latest</c>.</param>
    /// <param name="cancellationToken">A token to cancel the pull.</param>
    public async Task PullImageIfMissingAsync(string image, CancellationToken cancellationToken)
    {
        IList<ImagesListResponse> existing = await client.Images.ListImagesAsync(
            new ImagesListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>(StringComparer.Ordinal)
                {
                    ["reference"] = new Dictionary<string, bool>(StringComparer.Ordinal) { [image] = true },
                },
            },
            cancellationToken);
        if (existing.Count > 0)
        {
            return;
        }

        await client.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = image },
            authConfig: null,
            new Progress<JSONMessage>(),
            cancellationToken);
    }

    /// <summary>Creates a Docker network outside DCP's management.</summary>
    /// <param name="name">The network name.</param>
    /// <param name="cancellationToken">A token to cancel creation.</param>
    /// <returns>The created network's id (usable as a <see cref="HostConfig.NetworkMode"/> value).</returns>
    public async Task<string> CreateNetworkAsync(string name, CancellationToken cancellationToken)
    {
        NetworksCreateResponse response = await client.Networks.CreateNetworkAsync(
            new NetworksCreateParameters { Name = name }, cancellationToken);
        networkIds.Add(response.ID);
        return response.ID;
    }

    /// <summary>Creates and starts a container outside DCP's management.</summary>
    /// <param name="image">The image to run (must already be present — see <see cref="PullImageIfMissingAsync"/>).</param>
    /// <param name="labels">Docker labels, e.g. the DockYarp discovery labels (<c>VIRTUAL_HOST</c>, …).</param>
    /// <param name="hostConfig">Host configuration (e.g. <see cref="HostConfig.NetworkMode"/> set to
    /// <c>"host"</c> or a network id from <see cref="CreateNetworkAsync"/>); <see langword="null"/> uses
    /// Docker's default bridge network.</param>
    /// <param name="cancellationToken">A token to cancel creation/start.</param>
    /// <param name="env">Environment variables (e.g. <c>ASPNETCORE_URLS</c>, <c>BACKEND_ID</c>);
    /// <see langword="null"/> uses the image's own defaults.</param>
    /// <returns>The created container's id.</returns>
    public async Task<string> RunContainerAsync(
        string image,
        IDictionary<string, string> labels,
        HostConfig? hostConfig,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? env)
    {
        List<string> envList = env is null ? [] : [.. env.Select(pair => $"{pair.Key}={pair.Value}")];
        CreateContainerResponse created = await client.Containers.CreateContainerAsync(
            new CreateContainerParameters { Image = image, Labels = labels, HostConfig = hostConfig, Env = envList },
            cancellationToken);
        containerIds.Add(created.ID);
        await client.Containers.StartContainerAsync(created.ID, new ContainerStartParameters(), cancellationToken);
        return created.ID;
    }

    /// <summary>Connects a container to the Aspire session's own Docker network.</summary>
    /// <param name="containerId">The container to connect (typically one started by <see cref="RunContainerAsync"/>).</param>
    /// <param name="cancellationToken">A token to cancel the lookup/connect.</param>
    /// <remarks>
    /// DockYarp only routes a discovered container that shares a network with the proxy's own auto-detected
    /// reachable set (<c>Docker:ProxyNetworks</c> unset) — a container with no such shared network is excluded
    /// from <c>/api/routes</c> entirely, not merely marked unreachable. Aspire/DCP names its one per-session
    /// network <c>aspire-session-network-&lt;id&gt;-&lt;app&gt;</c> (observed directly from
    /// <c>DiscoveryReconciler</c>'s own "Detected the proxy's own networks" log line); a scenario that needs the
    /// proxy to actually discover a non-DCP container — as opposed to a deliberately unreachable one (see
    /// <c>e2e-nondcp-network-scenarios</c>) — connects it here after creation.
    /// </remarks>
    public async Task ConnectToAspireSessionNetworkAsync(string containerId, CancellationToken cancellationToken)
    {
        IList<NetworkResponse> networks = await client.Networks.ListNetworksAsync(
            new NetworksListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>(StringComparer.Ordinal)
                {
                    ["name"] = new Dictionary<string, bool>(StringComparer.Ordinal) { ["aspire-session-network"] = true },
                },
            },
            cancellationToken);
        if (networks.Count == 0)
        {
            throw new InvalidOperationException("No Aspire session network found to connect the container to.");
        }

        await client.Networks.ConnectNetworkAsync(
            networks[0].ID, new NetworkConnectParameters { Container = containerId }, cancellationToken);
    }

    /// <summary>Removes every container and network this instance created, best-effort.</summary>
    /// <remarks>
    /// A single removal failing (e.g. the resource was already gone) must not stop the rest from being
    /// attempted — mirrors <c>AspireAppHostFixture</c>'s own teardown robustness. Containers are removed before
    /// networks: a network with an attached container cannot be deleted.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        foreach (string id in containerIds)
        {
            try
            {
                await client.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters { Force = true });
            }
            catch (DockerApiException)
            {
                // Best-effort cleanup: the container may already be gone (e.g. the daemon reaped it), and one
                // failed removal must not prevent the others from being attempted.
            }
        }

        foreach (string id in networkIds)
        {
            try
            {
                await client.Networks.DeleteNetworkAsync(id);
            }
            catch (DockerApiException)
            {
                // Best-effort cleanup — see the container-removal catch above for the same rationale.
            }
        }

        client.Dispose();
    }
}
