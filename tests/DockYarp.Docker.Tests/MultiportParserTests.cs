namespace DockYarp.Docker.Tests;

using System.Collections.Immutable;
using System.Linq;

using AwesomeAssertions;

using DockYarp.Core.Models;
using DockYarp.Docker.Labels;
using DockYarp.Docker.Models;

/// <summary>Tests for <see cref="MultiportParser"/>.</summary>
public sealed class MultiportParserTests
{
    private const string Yaml =
        """
        app.local:
          "/":
            port: 8080
          "/api":
            port: 9000
            proto: https
            dest: "/"
        """;

    /// <summary>The YAML mapping is parsed into per-host/path entries.</summary>
    [Test]
    public void ParsesEntries()
    {
        MultiportParser.TryParse(Yaml, out ImmutableArray<MultiportEntry> entries, out string? error).Should().BeTrue();
        error.Should().BeNull();

        entries.Should().HaveCount(2);
        MultiportEntry api = entries.Single(entry => entry.Path == "/api");
        api.Host.Should().Be("app.local");
        api.Port.Should().Be(9000);
        api.Scheme.Should().Be(BackendScheme.Https);
        api.Dest.Should().Be("/");
        entries.Single(entry => entry.Path == "/").Scheme.Should().Be(BackendScheme.Http);
    }

    /// <summary>Invalid YAML is reported as a failure.</summary>
    [Test]
    public void InvalidYamlFails()
    {
        MultiportParser.TryParse("app.local:\n  \"/\": [1, 2", out ImmutableArray<MultiportEntry> entries, out string? error)
            .Should().BeFalse();

        error.Should().NotBeNull();
        entries.Should().BeEmpty();
    }
}
