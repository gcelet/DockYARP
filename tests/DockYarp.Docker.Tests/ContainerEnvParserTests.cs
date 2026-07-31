namespace DockYarp.Docker.Tests;

using System.Collections.Generic;

using AwesomeAssertions;

using DockYarp.Docker.Discovery;

/// <summary>Tests for <see cref="ContainerEnvParser"/>.</summary>
public sealed class ContainerEnvParserTests
{
    /// <summary>KEY=VALUE entries are parsed; a value may itself contain '='.</summary>
    [Test]
    public void ParsesKeyValueEntries()
    {
        IReadOnlyDictionary<string, string> env = ContainerEnvParser.Parse(
            ["VIRTUAL_HOST=app.local", "VIRTUAL_PORT=8080", "TOKEN=a=b=c"]);

        env["VIRTUAL_HOST"].Should().Be("app.local");
        env["VIRTUAL_PORT"].Should().Be("8080");
        env["TOKEN"].Should().Be("a=b=c");
    }

    /// <summary>Entries with no key (no '=', or a leading '=') are skipped; keys are case-sensitive.</summary>
    [Test]
    public void SkipsEntriesWithoutAKey()
    {
        IReadOnlyDictionary<string, string> env = ContainerEnvParser.Parse(
            ["PLAIN", "=novalue", "VIRTUAL_HOST=app.local", "EMPTY="]);

        env.Keys.Should().BeEquivalentTo("VIRTUAL_HOST", "EMPTY");
        env["EMPTY"].Should().BeEmpty();
        env.Should().NotContainKey("PLAIN");
    }

    /// <summary>Null or empty input yields an empty map.</summary>
    [Test]
    public void NullOrEmptyYieldsEmpty()
    {
        ContainerEnvParser.Parse(null).Should().BeEmpty();
        ContainerEnvParser.Parse([]).Should().BeEmpty();
    }
}
