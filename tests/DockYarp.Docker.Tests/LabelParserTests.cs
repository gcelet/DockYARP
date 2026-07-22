namespace DockYarp.Docker.Tests;

using AwesomeAssertions;

using DockYarp.Docker.Labels;
using DockYarp.Docker.Models;

/// <summary>Tests for <see cref="LabelParser"/>.</summary>
public sealed class LabelParserTests
{
    /// <summary>A host and explicit port produce a routable configuration.</summary>
    [Test]
    public void HostAndPortAreParsed()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080")));

        bool parsed = LabelParser.TryParse(container, out ContainerLabelConfig? config, out string? error);

        parsed.Should().BeTrue();
        error.Should().BeNull();
        config!.Host.Should().Be("app.local");
        config.Port.Should().Be(8080);
    }

    /// <summary>A missing VIRTUAL_HOST makes the container invalid.</summary>
    [Test]
    public void MissingHostIsInvalid()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels((DockerLabels.VirtualPort, "8080")));

        bool parsed = LabelParser.TryParse(container, out ContainerLabelConfig? config, out string? error);

        parsed.Should().BeFalse();
        config.Should().BeNull();
        error.Should().NotBeNull();
    }

    /// <summary>When a single port is exposed and VIRTUAL_PORT is absent, that port is inferred.</summary>
    [Test]
    public void PortInferredFromSingleExposedPort()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local")),
            8080);

        bool parsed = LabelParser.TryParse(container, out ContainerLabelConfig? config, out _);

        parsed.Should().BeTrue();
        config!.Port.Should().Be(8080);
    }

    /// <summary>With several exposed ports and no VIRTUAL_PORT, the container is invalid.</summary>
    [Test]
    public void AmbiguousPortsRequireExplicitPort()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local")),
            80,
            8080);

        bool parsed = LabelParser.TryParse(container, out ContainerLabelConfig? config, out string? error);

        parsed.Should().BeFalse();
        config.Should().BeNull();
        error.Should().NotBeNull();
    }

    /// <summary>LETSENCRYPT labels are parsed into the configuration.</summary>
    [Test]
    public void LetsEncryptLabelsAreParsed()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.LetsEncryptHost, "app.local"),
                (DockerLabels.LetsEncryptEmail, "admin@example.com")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.LetsEncryptHost.Should().Be("app.local");
        config.LetsEncryptEmail.Should().Be("admin@example.com");
    }

    /// <summary>Complete auth labels produce Basic Auth credentials.</summary>
    [Test]
    public void AuthLabelsAreParsed()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.AuthUser, "admin"),
                (DockerLabels.AuthPassword, "secret"),
                (DockerLabels.AuthRealm, "app")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.Auth.Should().NotBeNull();
        config.Auth!.Username.Should().Be("admin");
        config.Auth.Realm.Should().Be("app");
    }

    /// <summary>Partial auth labels produce no credentials and are flagged as incomplete.</summary>
    [Test]
    public void PartialAuthYieldsNoCredentials()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.AuthUser, "admin")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.Auth.Should().BeNull();
        LabelParser.HasIncompleteAuth(container.Labels).Should().BeTrue();
    }
}
