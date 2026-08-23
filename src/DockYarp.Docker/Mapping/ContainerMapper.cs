namespace DockYarp.Docker.Mapping;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using DockYarp.Core.Configuration;
using DockYarp.Core.Models;
using DockYarp.Docker.Labels;
using DockYarp.Docker.Models;

/// <summary>Maps discovered containers into a dynamic <see cref="ConfigContribution"/>.</summary>
/// <remarks>
/// Containers sharing a <c>VIRTUAL_HOST</c> are aggregated into a single cluster (one endpoint per
/// container, keyed by container id). A container with <c>VIRTUAL_HOST_MULTIPORTS</c> instead contributes one
/// route/cluster per host/path entry. Invalid containers are skipped and reported as warnings.
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
        Dictionary<string, HostGroup> hostGroups = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, MultiportGroup> multiportGroups = new(StringComparer.OrdinalIgnoreCase);

        foreach (ContainerInfo container in containers)
        {
            // Health-aware: an unhealthy/starting container is excluded so healthy siblings still serve the host.
            if (container.Health is ContainerHealth.Unhealthy or ContainerHealth.Starting)
            {
                warnings.Add($"{container.Name} ({Short(container.Id)}): excluded while {container.Health} (not routed).");
                continue;
            }

            // No reachable address (e.g. host mode without a host address, or the proxy shares none of the
            // container's networks): skip rather than build a broken scheme://:port endpoint.
            if (string.IsNullOrEmpty(container.Address))
            {
                string reason = container.IsHostNetwork
                    ? "host-network container requires Docker:HostAddress"
                    : "no reachable network address";
                warnings.Add($"{container.Name} ({Short(container.Id)}): {reason}; not routed.");
                continue;
            }

            // Effective config = labels overlaid by environment variables (env wins); read all knobs from it.
            IReadOnlyDictionary<string, string> config = LabelParser.EffectiveConfig(container);
            if (config.ContainsKey(DockerLabels.VirtualHostMultiports))
            {
                ProcessMultiports(container, config, multiportGroups, warnings);
            }
            else
            {
                ProcessClassic(container, config, hostGroups, warnings);
            }
        }

        ImmutableArray<RouteRule>.Builder routes = ImmutableArray.CreateBuilder<RouteRule>();
        ImmutableArray<Cluster>.Builder clusters = ImmutableArray.CreateBuilder<Cluster>();
        foreach (KeyValuePair<string, HostGroup> group in hostGroups)
        {
            routes.Add(group.Value.BuildRoute(group.Key));
            clusters.Add(group.Value.BuildCluster(group.Key));
        }

        foreach (KeyValuePair<string, MultiportGroup> group in multiportGroups)
        {
            routes.Add(group.Value.BuildRoute(group.Key));
            clusters.Add(group.Value.BuildCluster(group.Key));
        }

        ConfigContribution contribution = new(ConfigSource.Dynamic, routes.ToImmutable(), clusters.ToImmutable());
        return new ContainerMapResult(contribution, warnings.ToImmutable());
    }

    private static void ProcessClassic(
        ContainerInfo container,
        IReadOnlyDictionary<string, string> effectiveConfig,
        Dictionary<string, HostGroup> groups,
        ImmutableArray<string>.Builder warnings)
    {
        if (!LabelParser.TryParse(container, out ContainerLabelConfig? config, out string? error))
        {
            warnings.Add($"{container.Name} ({Short(container.Id)}): {error}");
            return;
        }

        AddCommonWarnings(container, effectiveConfig, warnings);
        if (LabelParser.HasUnsupportedProto(effectiveConfig))
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

    private static void ProcessMultiports(
        ContainerInfo container,
        IReadOnlyDictionary<string, string> effectiveConfig,
        Dictionary<string, MultiportGroup> groups,
        ImmutableArray<string>.Builder warnings)
    {
        string yaml = effectiveConfig[DockerLabels.VirtualHostMultiports];
        if (!MultiportParser.TryParse(yaml, out ImmutableArray<MultiportEntry> entries, out string? error))
        {
            warnings.Add($"{container.Name} ({Short(container.Id)}): invalid {DockerLabels.VirtualHostMultiports}: {error}");
            return;
        }

        if (entries.IsEmpty)
        {
            warnings.Add($"{container.Name} ({Short(container.Id)}): {DockerLabels.VirtualHostMultiports} has no valid entries.");
            return;
        }

        AddCommonWarnings(container, effectiveConfig, warnings);
        ContainerLabelConfig common = LabelParser.ParseCommon(effectiveConfig);
        foreach (MultiportEntry entry in entries)
        {
            string clusterId = ClusterId(entry.Host, entry.Path);
            if (!groups.TryGetValue(clusterId, out MultiportGroup? group))
            {
                group = new MultiportGroup(entry.Host, entry.Path, entry.Dest, common);
                groups.Add(clusterId, group);
            }

            group.Add(container, entry);
        }
    }

    private static void AddCommonWarnings(
        ContainerInfo container, IReadOnlyDictionary<string, string> config, ImmutableArray<string>.Builder warnings)
    {
        string id = Short(container.Id);
        if (LabelParser.HasIncompleteAuth(config))
        {
            warnings.Add($"{container.Name} ({id}): incomplete auth labels; route left unprotected.");
        }

        if (LabelParser.HasInvalidPriority(config))
        {
            warnings.Add($"{container.Name} ({id}): invalid {DockerLabels.Priority}; using priority 0.");
        }

        if (LabelParser.HasUnsupportedHttpsMethod(config))
        {
            warnings.Add($"{container.Name} ({id}): unrecognized {DockerLabels.HttpsMethod}; using redirect.");
        }

        if (LabelParser.HasUnsupportedLoadBalancing(config))
        {
            warnings.Add($"{container.Name} ({id}): unrecognized {DockerLabels.LoadBalancing}; using round-robin.");
        }

        if (LabelParser.HasUnsupportedAffinity(config))
        {
            warnings.Add($"{container.Name} ({id}): unrecognized {DockerLabels.SessionAffinity}; affinity not applied.");
        }

        if (LabelParser.HasUnsupportedClientCert(config))
        {
            warnings.Add($"{container.Name} ({id}): unrecognized {DockerLabels.ClientCert}; requiring no client certificate.");
        }

        if (LabelParser.HasUnsupportedAcmeChallenge(config))
        {
            warnings.Add($"{container.Name} ({id}): unrecognized {DockerLabels.AcmeChallenge}; using http-01.");
        }

        if (LabelParser.HasInvalidProxyTimeout(config))
        {
            warnings.Add($"{container.Name} ({id}): invalid {DockerLabels.ProxyTimeout}; no timeout applied.");
        }

        if (LabelParser.HasInvalidMaxBodySize(config))
        {
            warnings.Add($"{container.Name} ({id}): invalid {DockerLabels.MaxBodySize}; no body-size limit applied.");
        }

        if (LabelParser.HasInvalidExternalHttpsPort(config))
        {
            warnings.Add($"{container.Name} ({id}): invalid {DockerLabels.ExternalHttpsPort}; the default HTTPS port is used.");
        }

        if (LabelParser.HasInvalidMaxConnections(config))
        {
            warnings.Add($"{container.Name} ({id}): invalid {DockerLabels.MaxConnections}; default connection pooling is used.");
        }
    }

    private static string ClusterId(string host, string path) =>
        RoutePath(path) is { } routePath ? string.Concat(host, routePath) : host;

    private static string? RoutePath(string path) =>
        string.IsNullOrEmpty(path) || path == "/" ? null : path;

    private static string Short(string id) => id.Length <= 12 ? id : id[..12];

    private sealed class HostGroup(ContainerLabelConfig first)
    {
        private readonly List<ClusterEndpoint> endpoints = [];

        public void Add(ContainerInfo container, ContainerLabelConfig config) =>
            endpoints.Add(ClusterEndpoint.Create(container.Id, config.Scheme, container.Address, config.Port));

        public RouteRule BuildRoute(string host)
        {
            // A host with LETSENCRYPT_HOST or a CERT_NAME shared certificate is served over HTTPS; a CERT_NAME-only
            // host certifies the vhost itself (no ACME).
            string? letsEncryptHost = first.LetsEncryptHost is { Length: > 0 } ? first.LetsEncryptHost : null;
            HostTlsMetadata? tls = letsEncryptHost is not null || first.CertName is { Length: > 0 }
                ? new HostTlsMetadata
                {
                    CertificateHost = letsEncryptHost ?? host,
                    ContactEmail = first.LetsEncryptEmail,
                    ChallengeType = first.ChallengeType,
                    Method = first.HttpsMethod,
                    Hsts = first.Hsts,
                    CertificateName = first.CertName,
                    SslPolicy = first.SslPolicy,
                    Http2Enabled = first.Http2Enabled,
                    ExternalHttpsPort = first.ExternalHttpsPort,
                    EnableHttpOnMissingCert = first.EnableHttpOnMissingCert,
                    TrustDefaultCert = first.TrustDefaultCert,
                }
                : null;

            RouteTransforms? transforms =
                first.PathRemovePrefix is { Length: > 0 } || first.PathAddPrefix is { Length: > 0 }
                    ? new RouteTransforms { PathRemovePrefix = first.PathRemovePrefix, PathAddPrefix = first.PathAddPrefix }
                    : null;

            return new RouteRule
            {
                HostPattern = host,
                PathPrefix = first.PathPrefix,
                Priority = first.Priority,
                ClusterId = host,
                Tls = tls,
                Auth = first.Auth,
                ClientCertificate = first.ClientCertificate,
                MaxRequestBodySize = first.MaxRequestBodySize,
                InternalOnly = first.InternalOnly,
                ServerTokens = first.ServerTokens,
                Transforms = transforms,
            };
        }

        public Cluster BuildCluster(string host) =>
            new()
            {
                Id = host,
                Endpoints = [.. endpoints],
                LoadBalancingPolicy = first.LoadBalancingPolicy ?? LoadBalancingPolicy.RoundRobin,
                SessionAffinityPolicy = first.SessionAffinityPolicy ?? SessionAffinityPolicy.None,
                RequestTimeout = first.ProxyTimeout,
                Http2Only = first.Http2,
                MaxConnectionsPerServer = first.MaxConnectionsPerServer,
            };
    }

    private sealed class MultiportGroup(string host, string path, string? dest, ContainerLabelConfig common)
    {
        private readonly List<ClusterEndpoint> endpoints = [];

        private static bool LetsEncryptCovers(string? letsEncryptHost, string host) =>
            letsEncryptHost is { Length: > 0 } list
            && list.Split(',').Any(candidate => string.Equals(candidate.Trim(), host, StringComparison.OrdinalIgnoreCase));

        public void Add(ContainerInfo container, MultiportEntry entry) =>
            endpoints.Add(ClusterEndpoint.Create(container.Id, entry.Scheme, container.Address, entry.Port));

        public RouteRule BuildRoute(string clusterId)
        {
            string? routePath = RoutePath(path);
            HostTlsMetadata? tls = LetsEncryptCovers(common.LetsEncryptHost, host) || common.CertName is { Length: > 0 }
                ? new HostTlsMetadata
                {
                    CertificateHost = host,
                    ContactEmail = common.LetsEncryptEmail,
                    ChallengeType = common.ChallengeType,
                    Method = common.HttpsMethod,
                    Hsts = common.Hsts,
                    CertificateName = common.CertName,
                    SslPolicy = common.SslPolicy,
                    Http2Enabled = common.Http2Enabled,
                    ExternalHttpsPort = common.ExternalHttpsPort,
                    EnableHttpOnMissingCert = common.EnableHttpOnMissingCert,
                    TrustDefaultCert = common.TrustDefaultCert,
                }
                : null;

            // VIRTUAL_DEST strips the matched path prefix and, for a non-root dest, prepends the destination.
            (string? remove, string? add) = PathRewrite.Resolve(dest, routePath);
            RouteTransforms? transforms = remove is { Length: > 0 } || add is { Length: > 0 }
                ? new RouteTransforms { PathRemovePrefix = remove, PathAddPrefix = add }
                : null;

            return new RouteRule
            {
                HostPattern = host,
                PathPrefix = routePath,
                Priority = common.Priority,
                ClusterId = clusterId,
                Tls = tls,
                Auth = common.Auth,
                ClientCertificate = common.ClientCertificate,
                MaxRequestBodySize = common.MaxRequestBodySize,
                InternalOnly = common.InternalOnly,
                ServerTokens = common.ServerTokens,
                Transforms = transforms,
            };
        }

        public Cluster BuildCluster(string clusterId) =>
            new()
            {
                Id = clusterId,
                Endpoints = [.. endpoints],
                LoadBalancingPolicy = common.LoadBalancingPolicy ?? LoadBalancingPolicy.RoundRobin,
                SessionAffinityPolicy = common.SessionAffinityPolicy ?? SessionAffinityPolicy.None,
                RequestTimeout = common.ProxyTimeout,
                MaxConnectionsPerServer = common.MaxConnectionsPerServer,
            };
    }
}
