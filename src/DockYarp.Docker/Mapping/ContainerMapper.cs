namespace DockYarp.Docker.Mapping;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using DockYarp.Core.Configuration;
using DockYarp.Core.Models;
using DockYarp.Docker.Labels;
using DockYarp.Docker.Models;

/// <summary>Maps discovered containers into a dynamic <see cref="ConfigContribution"/>.</summary>
/// <remarks>
/// Containers sharing a <c>VIRTUAL_HOST</c> are aggregated into a single cluster (one endpoint per
/// container, keyed by container id). Invalid containers are skipped and reported as warnings.
/// </remarks>
public static class ContainerMapper
{
    /// <summary>Maps the given containers.</summary>
    /// <param name="containers">The discovered containers.</param>
    /// <returns>The dynamic contribution plus warnings for skipped containers.</returns>
    public static ContainerMapResult Map(IReadOnlyList<ContainerInfo> containers)
    {
        ArgumentNullException.ThrowIfNull(containers);

        ImmutableArray<string>.Builder warnings = ImmutableArray.CreateBuilder<string>();
        Dictionary<string, HostGroup> groups = GroupByHost(containers, warnings);

        ImmutableArray<RouteRule>.Builder routes = ImmutableArray.CreateBuilder<RouteRule>();
        ImmutableArray<Cluster>.Builder clusters = ImmutableArray.CreateBuilder<Cluster>();
        foreach (KeyValuePair<string, HostGroup> group in groups)
        {
            routes.Add(group.Value.BuildRoute(group.Key));
            clusters.Add(group.Value.BuildCluster(group.Key));
        }

        ConfigContribution contribution = new(ConfigSource.Dynamic, routes.ToImmutable(), clusters.ToImmutable());
        return new ContainerMapResult(contribution, warnings.ToImmutable());
    }

    private static Dictionary<string, HostGroup> GroupByHost(
        IReadOnlyList<ContainerInfo> containers,
        ImmutableArray<string>.Builder warnings)
    {
        Dictionary<string, HostGroup> groups = new(StringComparer.OrdinalIgnoreCase);
        foreach (ContainerInfo container in containers)
        {
            if (!LabelParser.TryParse(container, out ContainerLabelConfig? config, out string? error))
            {
                warnings.Add($"{container.Name} ({Short(container.Id)}): {error}");
                continue;
            }

            if (LabelParser.HasIncompleteAuth(container.Labels))
            {
                warnings.Add($"{container.Name} ({Short(container.Id)}): incomplete auth labels; route left unprotected.");
            }

            if (LabelParser.HasUnsupportedProto(container.Labels))
            {
                warnings.Add($"{container.Name} ({Short(container.Id)}): unsupported {DockerLabels.VirtualProto}; defaulting to http.");
            }

            // A comma-separated VIRTUAL_HOST fans the container out to one route/cluster per host.
            foreach (string host in config.Hosts)
            {
                if (!groups.TryGetValue(host, out HostGroup? group))
                {
                    group = new HostGroup(config);
                    groups.Add(host, group);
                }

                group.Add(container, config);
            }
        }

        return groups;
    }

    private static string Short(string id) => id.Length <= 12 ? id : id[..12];

    private sealed class HostGroup(ContainerLabelConfig first)
    {
        private readonly List<ClusterEndpoint> endpoints = [];

        public void Add(ContainerInfo container, ContainerLabelConfig config) =>
            endpoints.Add(ClusterEndpoint.Create(container.Id, config.Scheme, container.Address, config.Port));

        public RouteRule BuildRoute(string host)
        {
            HostTlsMetadata? tls = first.LetsEncryptHost is { Length: > 0 } certificateHost
                ? new HostTlsMetadata
                {
                    CertificateHost = certificateHost,
                    ContactEmail = first.LetsEncryptEmail,
                    EnforceHttps = true,
                }
                : null;

            RouteTransforms? transforms = first.PathRemovePrefix is { Length: > 0 } prefix
                ? new RouteTransforms { PathRemovePrefix = prefix }
                : null;

            return new RouteRule
            {
                HostPattern = host,
                PathPrefix = first.PathPrefix,
                ClusterId = host,
                Tls = tls,
                Auth = first.Auth,
                Transforms = transforms,
            };
        }

        public Cluster BuildCluster(string host) =>
            new()
            {
                Id = host,
                Endpoints = [.. endpoints],
                LoadBalancingPolicy = first.LoadBalancingPolicy ?? LoadBalancingPolicy.RoundRobin,
            };
    }
}
