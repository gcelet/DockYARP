namespace DockYarp.Docker.Tests;

using System;
using System.Collections.Generic;

using DockYarp.Docker.Models;

/// <summary>Helpers to build container fixtures for discovery tests.</summary>
internal static class DiscoveryTestData
{
    public static ContainerInfo Container(
        string id,
        string address,
        IReadOnlyDictionary<string, string> labels,
        params int[] ports) =>
        new()
        {
            Id = id,
            Name = id,
            Address = address,
            Labels = labels,
            ExposedPorts = [.. ports],
        };

    public static Dictionary<string, string> Labels(params (string Key, string Value)[] entries)
    {
        Dictionary<string, string> labels = new(StringComparer.Ordinal);
        foreach ((string Key, string Value) entry in entries)
        {
            labels[entry.Key] = entry.Value;
        }

        return labels;
    }
}
