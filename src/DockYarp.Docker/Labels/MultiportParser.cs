namespace DockYarp.Docker.Labels;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

using DockYarp.Core.Models;
using DockYarp.Docker.Models;

using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>Parses a <c>VIRTUAL_HOST_MULTIPORTS</c> YAML value into per-entry mappings.</summary>
/// <remarks>Pure and side-effect free so it can be unit tested without a Docker daemon.</remarks>
public static class MultiportParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>Attempts to parse the YAML mapping into host/path/port entries.</summary>
    /// <param name="yaml">The <c>VIRTUAL_HOST_MULTIPORTS</c> value.</param>
    /// <param name="entries">The parsed entries when successful.</param>
    /// <param name="error">A human-readable reason when parsing fails.</param>
    /// <returns><see langword="true"/> when the value is valid YAML (even if it yields no entries).</returns>
    public static bool TryParse(
        string yaml,
        out ImmutableArray<MultiportEntry> entries,
        [NotNullWhen(false)] out string? error)
    {
        entries = [];
        Dictionary<string, Dictionary<string, PathSpec>>? document;
        try
        {
            document = Deserializer.Deserialize<Dictionary<string, Dictionary<string, PathSpec>>>(yaml);
        }
        catch (YamlException exception)
        {
            error = exception.Message;
            return false;
        }

        error = null;
        if (document is null)
        {
            return true;
        }

        ImmutableArray<MultiportEntry>.Builder builder = ImmutableArray.CreateBuilder<MultiportEntry>();
        foreach ((string host, Dictionary<string, PathSpec>? paths) in document)
        {
            if (string.IsNullOrWhiteSpace(host) || paths is null)
            {
                continue;
            }

            foreach ((string path, PathSpec? spec) in paths)
            {
                if (spec is { Port: > 0 })
                {
                    builder.Add(new MultiportEntry(host.Trim(), path, spec.Port, ParseScheme(spec.Proto), spec.Dest));
                }
            }
        }

        entries = builder.ToImmutable();
        return true;
    }

    private static BackendScheme ParseScheme(string? proto) =>
        string.Equals(proto, "https", StringComparison.OrdinalIgnoreCase) ? BackendScheme.Https : BackendScheme.Http;

    [SuppressMessage(
        "SonarAnalyzer",
        "S1144:Unused private types or members should be removed",
        Justification = "Property accessors are invoked by YamlDotNet via reflection during deserialization.")]
    [SuppressMessage(
        "SonarAnalyzer",
        "S3459:Unassigned members should be removed",
        Justification = "Property values are assigned by YamlDotNet via reflection during deserialization.")]
    private sealed class PathSpec
    {
        public int Port { get; set; }

        public string? Dest { get; set; }

        public string? Proto { get; set; }
    }
}
