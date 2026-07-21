namespace DockYarp.Docker.Tests;

using System.Linq;

using AwesomeAssertions;

using DockYarp.Docker.Labels;
using DockYarp.Docker.Mapping;
using DockYarp.Docker.Models;

/// <summary>Tests for <see cref="ContainerMapper"/>.</summary>
public sealed class ContainerMapperTests
{
    /// <summary>Two containers sharing a host aggregate into one cluster with two endpoints.</summary>
    [Test]
    public void ReplicasAggregateIntoOneCluster()
    {
        ContainerInfo first = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080")));
        ContainerInfo second = DiscoveryTestData.Container(
            "c2",
            "10.0.0.2",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080")));

        ContainerMapResult result = ContainerMapper.Map([first, second]);

        result.Contribution.Routes.Should().ContainSingle(route => route.HostPattern == "app.local");
        result.Contribution.Clusters.Should().ContainSingle()
            .Which.Endpoints.Should().HaveCount(2);
    }

    /// <summary>LETSENCRYPT labels populate the route's TLS metadata.</summary>
    [Test]
    public void TlsMetadataIsPopulated()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.LetsEncryptHost, "app.local"),
                (DockerLabels.LetsEncryptEmail, "admin@example.com")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Single().Tls.Should().NotBeNull();
        result.Contribution.Routes.Single().Tls!.CertificateHost.Should().Be("app.local");
    }

    /// <summary>An invalid container is skipped and reported as a warning.</summary>
    [Test]
    public void InvalidContainerIsSkippedWithWarning()
    {
        ContainerInfo valid = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080")));
        ContainerInfo invalid = DiscoveryTestData.Container(
            "c2",
            "10.0.0.2",
            DiscoveryTestData.Labels((DockerLabels.VirtualPort, "8080")));

        ContainerMapResult result = ContainerMapper.Map([valid, invalid]);

        result.Contribution.Routes.Should().ContainSingle(route => route.HostPattern == "app.local");
        result.Warnings.Should().ContainSingle();
    }
}
