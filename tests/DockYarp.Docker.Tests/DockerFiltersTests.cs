namespace DockYarp.Docker.Tests;

using System;
using System.Collections.Generic;

using AwesomeAssertions;

using DockYarp.Docker.Discovery;

/// <summary>Tests for <see cref="DockerFilters"/>.</summary>
public sealed class DockerFiltersTests
{
    /// <summary>No configured filters produce no Docker filter (discover everything).</summary>
    [Test]
    public void EmptyFiltersProduceNull()
    {
        DockerFilters.Build(new Dictionary<string, IList<string>>(StringComparer.Ordinal)).Should().BeNull();
        DockerFilters.Build(null).Should().BeNull();
    }

    /// <summary>A single key/value maps to the Docker inclusion shape.</summary>
    [Test]
    public void SingleKeyValueMaps()
    {
        Dictionary<string, IList<string>> filters = new(StringComparer.Ordinal)
        {
            ["label"] = ["dockyarp.enable=true"],
        };

        IDictionary<string, IDictionary<string, bool>>? result = DockerFilters.Build(filters);

        result.Should().NotBeNull();
        result.Should().ContainKey("label");
        result["label"].Should().ContainKey("dockyarp.enable=true").WhoseValue.Should().BeTrue();
    }

    /// <summary>Multiple values under one key are all included (OR within a key).</summary>
    [Test]
    public void MultipleValuesUnderOneKey()
    {
        Dictionary<string, IList<string>> filters = new(StringComparer.Ordinal)
        {
            ["network"] = ["edge", "internal"],
        };

        IDictionary<string, IDictionary<string, bool>>? result = DockerFilters.Build(filters);

        result!["network"].Keys.Should().BeEquivalentTo("edge", "internal");
    }

    /// <summary>Distinct keys are preserved as separate filter entries (AND across keys).</summary>
    [Test]
    public void MultipleKeysPreserved()
    {
        Dictionary<string, IList<string>> filters = new(StringComparer.Ordinal)
        {
            ["label"] = ["dockyarp.enable=true"],
            ["name"] = ["web"],
        };

        IDictionary<string, IDictionary<string, bool>>? result = DockerFilters.Build(filters);

        result!.Keys.Should().BeEquivalentTo("label", "name");
    }

    /// <summary>Empty keys and blank values are dropped; a key left with no values is omitted.</summary>
    [Test]
    public void EmptyKeysAndValuesAreDropped()
    {
        Dictionary<string, IList<string>> filters = new(StringComparer.Ordinal)
        {
            [" "] = ["ignored"],
            ["label"] = ["", "  ", "keep=1"],
            ["name"] = [" "],
        };

        IDictionary<string, IDictionary<string, bool>>? result = DockerFilters.Build(filters);

        result!.Keys.Should().ContainSingle().Which.Should().Be("label");
        result["label"].Keys.Should().ContainSingle().Which.Should().Be("keep=1");
    }
}
