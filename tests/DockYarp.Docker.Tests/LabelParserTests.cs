namespace DockYarp.Docker.Tests;

using AwesomeAssertions;

using DockYarp.Core.Models;
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
        config!.Hosts.Should().ContainSingle().Which.Should().Be("app.local");
        config.Port.Should().Be(8080);
    }

    /// <summary>A comma-separated VIRTUAL_HOST yields one entry per host.</summary>
    [Test]
    public void CommaSeparatedHostsAreSplit()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local,www.app.local"),
                (DockerLabels.VirtualPort, "8080")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.Hosts.Should().Equal("app.local", "www.app.local");
    }

    /// <summary>Whitespace and empty entries in VIRTUAL_HOST are trimmed and dropped.</summary>
    [Test]
    public void WhitespaceAndEmptyHostEntriesAreTolerated()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "a.local, ,b.local"),
                (DockerLabels.VirtualPort, "8080")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.Hosts.Should().Equal("a.local", "b.local");
    }

    /// <summary>A VIRTUAL_HOST with only separators/whitespace leaves no valid host and is invalid.</summary>
    [Test]
    public void HostWithoutValidEntryIsInvalid()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, " , "), (DockerLabels.VirtualPort, "8080")));

        bool parsed = LabelParser.TryParse(container, out ContainerLabelConfig? config, out string? error);

        parsed.Should().BeFalse();
        config.Should().BeNull();
        error.Should().NotBeNull();
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

    /// <summary>A numeric priority label is parsed; absent defaults to zero.</summary>
    [Test]
    public void PriorityLabelIsParsed()
    {
        ContainerInfo withPriority = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.Priority, "10")));
        ContainerInfo withoutPriority = DiscoveryTestData.Container(
            "c2",
            "10.0.0.2",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080")));

        LabelParser.TryParse(withPriority, out ContainerLabelConfig? withConfig, out _).Should().BeTrue();
        LabelParser.TryParse(withoutPriority, out ContainerLabelConfig? withoutConfig, out _).Should().BeTrue();

        withConfig!.Priority.Should().Be(10);
        withoutConfig!.Priority.Should().Be(0);
    }

    /// <summary>A non-numeric priority falls back to zero and is flagged as invalid.</summary>
    [Test]
    public void NonNumericPriorityFallsBackToZero()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.Priority, "high")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.Priority.Should().Be(0);
        LabelParser.HasInvalidPriority(container.Labels).Should().BeTrue();
    }

    /// <summary>HTTPS_METHOD is parsed; absent defaults to redirect; an unknown value is flagged.</summary>
    [Test]
    public void HttpsMethodLabelIsParsed()
    {
        ContainerInfo noRedirect = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.HttpsMethod, "noredirect")));
        ContainerInfo defaulted = DiscoveryTestData.Container(
            "c2",
            "10.0.0.2",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080")));
        ContainerInfo bogus = DiscoveryTestData.Container(
            "c3",
            "10.0.0.3",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.HttpsMethod, "bogus")));

        LabelParser.TryParse(noRedirect, out ContainerLabelConfig? noRedirectConfig, out _).Should().BeTrue();
        LabelParser.TryParse(defaulted, out ContainerLabelConfig? defaultedConfig, out _).Should().BeTrue();

        noRedirectConfig!.HttpsMethod.Should().Be(HttpsMethod.NoRedirect);
        defaultedConfig!.HttpsMethod.Should().Be(HttpsMethod.Redirect);
        LabelParser.HasUnsupportedHttpsMethod(bogus.Labels).Should().BeTrue();
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
