namespace DockYarp.Docker.Labels;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using DockYarp.Core.Models;
using DockYarp.Docker.Models;

/// <summary>Parses a container's labels into a <see cref="ContainerLabelConfig"/>.</summary>
/// <remarks>Pure and side-effect free so it can be unit tested without a Docker daemon.</remarks>
public static class LabelParser
{
    /// <summary>Attempts to parse the container's labels into a configuration.</summary>
    /// <param name="container">The container to parse.</param>
    /// <param name="config">The parsed configuration when successful.</param>
    /// <param name="error">A human-readable reason when parsing fails.</param>
    /// <returns><see langword="true"/> when the container declares a valid configuration.</returns>
    public static bool TryParse(
        ContainerInfo container,
        [NotNullWhen(true)] out ContainerLabelConfig? config,
        [NotNullWhen(false)] out string? error)
    {
        config = null;
        IReadOnlyDictionary<string, string> labels = container.Labels;

        if (!labels.TryGetValue(DockerLabels.VirtualHost, out string? host) || string.IsNullOrWhiteSpace(host))
        {
            error = $"{DockerLabels.VirtualHost} is required.";
            return false;
        }

        if (!TryResolvePort(labels, container.ExposedPorts, out int port, out error))
        {
            return false;
        }

        error = null;
        config = new ContainerLabelConfig
        {
            Host = host,
            Port = port,
            PathPrefix = GetOrNull(labels, DockerLabels.VirtualPath),
            LetsEncryptHost = GetOrNull(labels, DockerLabels.LetsEncryptHost),
            LetsEncryptEmail = GetOrNull(labels, DockerLabels.LetsEncryptEmail),
            LoadBalancingPolicy = ParsePolicy(GetOrNull(labels, DockerLabels.LoadBalancing)),
        };
        return true;
    }

    private static bool TryResolvePort(
        IReadOnlyDictionary<string, string> labels,
        ImmutableArray<int> exposedPorts,
        out int port,
        [NotNullWhen(false)] out string? error)
    {
        error = null;
        port = 0;

        if (labels.TryGetValue(DockerLabels.VirtualPort, out string? raw))
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out port) && port > 0)
            {
                return true;
            }

            error = $"{DockerLabels.VirtualPort} '{raw}' is not a valid port.";
            return false;
        }

        if (exposedPorts.Length == 1)
        {
            port = exposedPorts[0];
            return true;
        }

        error = $"{DockerLabels.VirtualPort} is required because the container exposes {exposedPorts.Length} ports.";
        return false;
    }

    private static string? GetOrNull(IReadOnlyDictionary<string, string> labels, string key) =>
        labels.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static LoadBalancingPolicy? ParsePolicy(string? value)
    {
        return value?.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase).ToUpperInvariant() switch
        {
            "ROUNDROBIN" => LoadBalancingPolicy.RoundRobin,
            "LEASTREQUESTS" => LoadBalancingPolicy.LeastRequests,
            _ => null,
        };
    }
}
