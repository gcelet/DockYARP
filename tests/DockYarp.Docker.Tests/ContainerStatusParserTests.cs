namespace DockYarp.Docker.Tests;

using AwesomeAssertions;

using DockYarp.Docker.Discovery;
using DockYarp.Docker.Models;

/// <summary>Tests for <see cref="ContainerStatusParser"/>.</summary>
public sealed class ContainerStatusParserTests
{
    /// <summary>A healthy status suffix parses to <see cref="ContainerHealth.Healthy"/>.</summary>
    [Test]
    public void ParsesHealthyStatus() =>
        ContainerStatusParser.ParseHealth("Up 2 minutes (healthy)").Should().Be(ContainerHealth.Healthy);

    /// <summary>An unhealthy status suffix parses to <see cref="ContainerHealth.Unhealthy"/>.</summary>
    [Test]
    public void ParsesUnhealthyStatus() =>
        ContainerStatusParser.ParseHealth("Up 5 minutes (unhealthy)").Should().Be(ContainerHealth.Unhealthy);

    /// <summary>A starting health suffix parses to <see cref="ContainerHealth.Starting"/>.</summary>
    [Test]
    public void ParsesStartingStatus() =>
        ContainerStatusParser.ParseHealth("Up 3 seconds (health: starting)").Should().Be(ContainerHealth.Starting);

    /// <summary>A status without a health suffix (or none at all) parses to <see cref="ContainerHealth.None"/>.</summary>
    [Test]
    public void NoHealthSuffixIsNone()
    {
        ContainerStatusParser.ParseHealth("Up 2 minutes").Should().Be(ContainerHealth.None);
        ContainerStatusParser.ParseHealth(null).Should().Be(ContainerHealth.None);
    }

    /// <summary>A <c>health_status</c> event action maps to <see cref="ContainerEventKind.Updated"/>.</summary>
    [Test]
    public void HealthStatusEventMapsToUpdated() =>
        ContainerStatusParser.MapAction("health_status: healthy").Should().Be(ContainerEventKind.Updated);

    /// <summary>Known lifecycle actions map to their kind; unknown actions map to null.</summary>
    [Test]
    public void KnownActionsMapAndUnknownIsNull()
    {
        ContainerStatusParser.MapAction("start").Should().Be(ContainerEventKind.Started);
        ContainerStatusParser.MapAction("die").Should().Be(ContainerEventKind.Died);
        ContainerStatusParser.MapAction("rename").Should().BeNull();
    }
}
