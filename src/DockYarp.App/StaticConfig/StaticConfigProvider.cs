namespace DockYarp.App.StaticConfig;

using System;
using System.Collections.Generic;
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
    private const string DefaultHost = "default";

    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ConfigContribution contribution;
    private readonly ConfigOverrides overrides;

    /// <summary>Loads the static configuration from the configured path.</summary>
    /// <param name="options">Static configuration options (file path).</param>
    /// <param name="fileSystem">Filesystem abstraction used to read the file.</param>
    /// <param name="logger">Logger for load outcomes.</param>
    public StaticConfigProvider(StaticConfigOptions options, IFileSystem fileSystem, ILogger<StaticConfigProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(logger);
        StaticConfigFile? file = Read(options, fileSystem, logger);
        contribution = file is null ? new ConfigContribution(ConfigSource.Static, [], []) : Map(file);
        overrides = file is null ? ConfigOverrides.Empty : MapOverrides(file);
    }

    /// <inheritdoc />
    public ConfigContribution GetContribution() => contribution;

    /// <inheritdoc />
    public ConfigOverrides GetOverrides() => overrides;

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Loading static config must never crash startup; any failure is logged and treated as no static config.")]
    private static StaticConfigFile? Read(StaticConfigOptions options, IFileSystem fileSystem, ILogger logger)
    {
        if (options.Path is not { Length: > 0 } path || !fileSystem.File.Exists(path))
        {
            return null;
        }

        try
        {
            StaticConfigFile? file = JsonSerializer.Deserialize<StaticConfigFile>(
                fileSystem.File.ReadAllText(path), SerializerOptions);
            if (file is null)
            {
                return null;
            }

            StaticConfigLog.Loaded(logger, file.Routes?.Length ?? 0, file.Clusters?.Length ?? 0, path);
            return file;
        }
        catch (Exception exception)
        {
            StaticConfigLog.LoadFailed(logger, path, exception.Message);
            return null;
        }
    }

    private static ConfigOverrides MapOverrides(StaticConfigFile file)
    {
        if (file.Overrides is not { Length: > 0 })
        {
            return ConfigOverrides.Empty;
        }

        Dictionary<string, IReadOnlyDictionary<string, string>> perHost = new(StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, string>? defaultHeaders = null;
        foreach (StaticConfigFile.OverrideEntry entry in file.Overrides)
        {
            if (entry.Host is not { Length: > 0 } host || entry.ResponseHeaders is not { Count: > 0 } headers)
            {
                continue;
            }

            IReadOnlyDictionary<string, string> copy = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
            if (string.Equals(host, DefaultHost, StringComparison.OrdinalIgnoreCase))
            {
                defaultHeaders = copy;
            }
            else
            {
                perHost[host] = copy;
            }
        }

        return new ConfigOverrides { ResponseHeadersByHost = perHost, DefaultResponseHeaders = defaultHeaders };
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
