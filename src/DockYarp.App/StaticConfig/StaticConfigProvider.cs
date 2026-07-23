namespace DockYarp.App.StaticConfig;

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;

using DockYarp.Core.Configuration;
using DockYarp.Core.Models;

using Microsoft.Extensions.Logging;

/// <summary>Reads the static configuration file into a <see cref="ConfigSource.Static"/> contribution.</summary>
/// <remarks>Loaded once at construction and cached; a missing or invalid file yields an empty contribution.</remarks>
public sealed class StaticConfigProvider : IStaticConfigProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ConfigContribution contribution;

    /// <summary>Loads the static configuration from the configured path.</summary>
    /// <param name="options">Static configuration options (file path).</param>
    /// <param name="fileSystem">Filesystem abstraction used to read the file.</param>
    /// <param name="logger">Logger for load outcomes.</param>
    public StaticConfigProvider(StaticConfigOptions options, IFileSystem fileSystem, ILogger<StaticConfigProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(logger);
        contribution = Load(options, fileSystem, logger);
    }

    /// <inheritdoc />
    public ConfigContribution GetContribution() => contribution;

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Loading static config must never crash startup; any failure is logged and treated as no static config.")]
    private static ConfigContribution Load(StaticConfigOptions options, IFileSystem fileSystem, ILogger logger)
    {
        ConfigContribution empty = new(ConfigSource.Static, [], []);
        if (options.Path is not { Length: > 0 } path || !fileSystem.File.Exists(path))
        {
            return empty;
        }

        try
        {
            StaticConfigFile? file = JsonSerializer.Deserialize<StaticConfigFile>(
                fileSystem.File.ReadAllText(path), SerializerOptions);
            if (file is null)
            {
                return empty;
            }

            ConfigContribution result = Map(file);
            StaticConfigLog.Loaded(logger, result.Routes.Length, result.Clusters.Length, path);
            return result;
        }
        catch (Exception exception)
        {
            StaticConfigLog.LoadFailed(logger, path, exception.Message);
            return empty;
        }
    }

    [SuppressMessage(
        "SonarAnalyzer",
        "S5332:Using http protocol is insecure",
        Justification = "Static backend addresses are operator-provided verbatim; scheme is their choice.")]
    private static ConfigContribution Map(StaticConfigFile file)
    {
        ImmutableArray<Cluster> clusters =
        [
            .. (file.Clusters ?? []).Select(entry => new Cluster
            {
                Id = entry.Id ?? string.Empty,
                Endpoints = [.. (entry.Addresses ?? []).Select(address => new ClusterEndpoint(address, address))],
                LoadBalancingPolicy = ParsePolicy(entry.LoadBalancing),
            }),
        ];
        ImmutableArray<RouteRule> routes =
        [
            .. (file.Routes ?? []).Select(entry => new RouteRule
            {
                HostPattern = entry.Host ?? string.Empty,
                PathPrefix = entry.Path,
                ClusterId = entry.Cluster ?? string.Empty,
                Priority = entry.Priority,
            }),
        ];
        return new ConfigContribution(ConfigSource.Static, routes, clusters);
    }

    private static LoadBalancingPolicy ParsePolicy(string? value) =>
        value?.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase).ToUpperInvariant() == "LEASTREQUESTS"
            ? LoadBalancingPolicy.LeastRequests
            : LoadBalancingPolicy.RoundRobin;
}
