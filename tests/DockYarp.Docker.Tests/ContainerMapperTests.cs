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

    /// <summary>A comma-separated VIRTUAL_HOST maps the container to one route/cluster per host.</summary>
    [Test]
    public void CommaSeparatedHostsCreateMultipleRoutes()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "a.local,b.local"),
                (DockerLabels.VirtualPort, "8080")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Select(route => route.HostPattern).Should().BeEquivalentTo("a.local", "b.local");
        result.Contribution.Clusters.Should().HaveCount(2);
        result.Contribution.Clusters.Should().OnlyContain(cluster => cluster.Endpoints.Length == 1);
    }

    /// <summary>VIRTUAL_PROTO=https makes the endpoint target the backend over HTTPS.</summary>
    [Test]
    public void HttpsProtoTargetsHttpsBackend()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "443"),
                (DockerLabels.VirtualProto, "https")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Clusters.Single().Endpoints.Single().Address.Should().Be("https://10.0.0.1:443");
    }

    /// <summary>An unsupported VIRTUAL_PROTO falls back to HTTP and produces a warning.</summary>
    [Test]
    public void UnsupportedProtoFallsBackToHttpWithWarning()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.VirtualProto, "fastcgi")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Clusters.Single().Endpoints.Single().Address.Should().Be("http://10.0.0.1:8080");
        result.Warnings.Should().Contain(warning => warning.Contains("VIRTUAL_PROTO", System.StringComparison.Ordinal));
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

    /// <summary>VIRTUAL_DEST with a VIRTUAL_PATH strips the path prefix before forwarding.</summary>
    [Test]
    public void VirtualDestStripsPathPrefix()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.VirtualPath, "/api"),
                (DockerLabels.VirtualDest, "/")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Single().Transforms.Should().NotBeNull();
        result.Contribution.Routes.Single().Transforms!.PathRemovePrefix.Should().Be("/api");
    }

    /// <summary>Without VIRTUAL_DEST no path transform is configured (original path forwarded).</summary>
    [Test]
    public void NoVirtualDestLeavesNoTransform()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.VirtualPath, "/api")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Single().Transforms.Should().BeNull();
    }

    /// <summary>A healthy container is routed.</summary>
    [Test]
    public void HealthyContainerIsRouted()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080")))
            with { Health = ContainerHealth.Healthy };

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Should().ContainSingle(route => route.HostPattern == "app.local");
    }

    /// <summary>An unhealthy container is excluded from routing and reported.</summary>
    [Test]
    public void UnhealthyContainerIsExcluded()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080")))
            with { Health = ContainerHealth.Unhealthy };

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Should().BeEmpty();
        result.Warnings.Should().Contain(warning => warning.Contains("Unhealthy", System.StringComparison.Ordinal));
    }

    /// <summary>A starting container is excluded until it becomes healthy.</summary>
    [Test]
    public void StartingContainerIsExcluded()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080")))
            with { Health = ContainerHealth.Starting };

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Should().BeEmpty();
    }

    /// <summary>An unhealthy replica is dropped while its healthy sibling still serves the host.</summary>
    [Test]
    public void HealthySiblingStillServesHost()
    {
        ContainerInfo healthy = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080")))
            with { Health = ContainerHealth.Healthy };
        ContainerInfo unhealthy = DiscoveryTestData.Container(
            "c2",
            "10.0.0.2",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080")))
            with { Health = ContainerHealth.Unhealthy };

        ContainerMapResult result = ContainerMapper.Map([healthy, unhealthy]);

        result.Contribution.Clusters.Should().ContainSingle()
            .Which.Endpoints.Should().ContainSingle(endpoint => endpoint.Id == "c1");
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

    /// <summary>Complete auth labels protect the mapped route.</summary>
    [Test]
    public void AuthLabelsProtectRoute()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.AuthUser, "admin"),
                (DockerLabels.AuthPassword, "secret")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Single().Auth.Should().NotBeNull();
        result.Contribution.Routes.Single().Auth!.Username.Should().Be("admin");
    }

    /// <summary>Incomplete auth labels leave the route unprotected and produce a warning.</summary>
    [Test]
    public void IncompleteAuthWarnsAndLeavesUnprotected()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.AuthUser, "admin")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Single().Auth.Should().BeNull();
        result.Warnings.Should().Contain(warning => warning.Contains("auth", System.StringComparison.Ordinal));
    }
}
