namespace DockYarp.Docker.Tests;

using System.Linq;

using AwesomeAssertions;

using DockYarp.Core.Models;
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

    /// <summary>A container configured only via environment variables is discovered and routed.</summary>
    [Test]
    public void EnvironmentConfiguredContainerIsRouted()
    {
        ContainerInfo container = DiscoveryTestData.Container("c1", "10.0.0.1", DiscoveryTestData.Labels())
            with
        {
            Env = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal)
            {
                [DockerLabels.VirtualHost] = "app.local",
                [DockerLabels.VirtualPort] = "8080",
            },
        };

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Should().ContainSingle(route => route.HostPattern == "app.local");
        result.Contribution.Clusters.Should().ContainSingle()
            .Which.Endpoints.Should().ContainSingle().Which.Address.Should().Be("http://10.0.0.1:8080");
    }

    /// <summary>CERT_NAME alone creates TLS metadata pinning the shared certificate (no LETSENCRYPT_HOST).</summary>
    [Test]
    public void CertNameCreatesTlsMetadata()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.CertName, "shared")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        HostTlsMetadata? tls = result.Contribution.Routes.Single().Tls;
        tls.Should().NotBeNull();
        tls!.CertificateName.Should().Be("shared");
        tls.CertificateHost.Should().Be("app.local");
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

    /// <summary>A container with no reachable network address is skipped with a warning, not routed.</summary>
    [Test]
    public void UnreachableContainerIsSkipped()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            string.Empty,
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Should().BeEmpty();
        result.Contribution.Clusters.Should().BeEmpty();
        result.Warnings.Should().Contain(warning => warning.Contains("no reachable network address", System.StringComparison.Ordinal));
    }

    /// <summary>An unrecognized DOCKYARP_LB is reported and the container still routes (round-robin default).</summary>
    [Test]
    public void UnknownLoadBalancingIsWarned()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.LoadBalancing, "ip-hash")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Clusters.Should().ContainSingle();
        result.Warnings.Should().Contain(warning => warning.Contains(DockerLabels.LoadBalancing, System.StringComparison.Ordinal));
    }

    /// <summary>A host-network container with a host address is routed to that address on its port.</summary>
    [Test]
    public void HostNetworkContainerIsRoutedToHostAddress()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "host.docker.internal",
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080")))
            with { IsHostNetwork = true };

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Clusters.Should().ContainSingle()
            .Which.Endpoints.Should().ContainSingle()
            .Which.Address.Should().Be("http://host.docker.internal:8080");
    }

    /// <summary>A host-network container with no host address is skipped with a Docker:HostAddress warning.</summary>
    [Test]
    public void HostNetworkContainerWithoutHostAddressIsSkipped()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            string.Empty,
            DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local"), (DockerLabels.VirtualPort, "8080")))
            with { IsHostNetwork = true };

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Should().BeEmpty();
        result.Warnings.Should().Contain(warning => warning.Contains("Docker:HostAddress", System.StringComparison.Ordinal));
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

    /// <summary>The priority label sets the route priority; an invalid value warns and defaults to zero.</summary>
    [Test]
    public void PriorityLabelSetsRoutePriority()
    {
        ContainerInfo valid = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.Priority, "7")));

        ContainerMapResult result = ContainerMapper.Map([valid]);

        result.Contribution.Routes.Single().Priority.Should().Be(7);
    }

    /// <summary>A non-numeric priority leaves priority at zero and produces a warning.</summary>
    [Test]
    public void InvalidPriorityWarnsAndDefaultsToZero()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.Priority, "high")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Single().Priority.Should().Be(0);
        result.Warnings.Should().Contain(warning => warning.Contains(DockerLabels.Priority, System.StringComparison.Ordinal));
    }

    /// <summary>HTTPS_METHOD is carried on the route's TLS metadata; an unknown value warns and defaults to redirect.</summary>
    [Test]
    public void HttpsMethodIsCarriedOnTlsMetadata()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.LetsEncryptHost, "app.local"),
                (DockerLabels.HttpsMethod, "noredirect")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Single().Tls!.Method.Should().Be(HttpsMethod.NoRedirect);
    }

    /// <summary>The HSTS label is carried onto the route's TLS metadata.</summary>
    [Test]
    public void HstsLabelIsCarriedOnTlsMetadata()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.LetsEncryptHost, "app.local"),
                (DockerLabels.Hsts, "off")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Single().Tls!.Hsts.Should().Be("off");
    }

    /// <summary>An unrecognized HTTPS_METHOD defaults to redirect and produces a warning.</summary>
    [Test]
    public void UnrecognizedHttpsMethodWarnsAndDefaultsToRedirect()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.LetsEncryptHost, "app.local"),
                (DockerLabels.HttpsMethod, "bogus")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Single().Tls!.Method.Should().Be(HttpsMethod.Redirect);
        result.Warnings.Should().Contain(warning => warning.Contains(DockerLabels.HttpsMethod, System.StringComparison.Ordinal));
    }

    /// <summary>DOCKYARP_CLIENT_CERT sets the route's client-certificate requirement.</summary>
    [Test]
    public void ClientCertLabelSetsRequirement()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.ClientCert, "required")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Single().ClientCertificate.Should().Be(ClientCertificateRequirement.Required);
    }

    /// <summary>An unrecognized DOCKYARP_CLIENT_CERT defaults to none and produces a warning.</summary>
    [Test]
    public void UnrecognizedClientCertWarnsAndDefaultsToNone()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.ClientCert, "maybe")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Single().ClientCertificate.Should().Be(ClientCertificateRequirement.None);
        result.Warnings.Should().Contain(warning => warning.Contains(DockerLabels.ClientCert, System.StringComparison.Ordinal));
    }

    /// <summary>Proxy-tuning labels set the cluster timeout and the route body-size limit.</summary>
    [Test]
    public void ProxyTuningLabelsAreCarried()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.ProxyTimeout, "30"),
                (DockerLabels.MaxBodySize, "1048576")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Clusters.Single().RequestTimeout.Should().Be(System.TimeSpan.FromSeconds(30));
        result.Contribution.Routes.Single().MaxRequestBodySize.Should().Be(1048576);
    }

    /// <summary>An invalid proxy-timeout value is ignored and reported.</summary>
    [Test]
    public void InvalidProxyTimeoutWarnsAndIsIgnored()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.ProxyTimeout, "soon")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Clusters.Single().RequestTimeout.Should().BeNull();
        result.Warnings.Should().Contain(warning => warning.Contains(DockerLabels.ProxyTimeout, System.StringComparison.Ordinal));
    }

    /// <summary>VIRTUAL_HOST_MULTIPORTS maps one host to several paths on different ports.</summary>
    [Test]
    public void MultiportsMapsMultiplePortsOnOneHost()
    {
        const string Yaml =
            """
            app.local:
              "/":
                port: 8080
              "/api":
                port: 9000
            """;
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels((DockerLabels.VirtualHostMultiports, Yaml)));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Should().HaveCount(2);
        result.Contribution.Routes.Should().Contain(route => route.PathPrefix == "/api" && route.ClusterId == "app.local/api");
        result.Contribution.Clusters.Single(cluster => cluster.Id == "app.local/api")
            .Endpoints.Single().Address.Should().Be("http://10.0.0.1:9000");
    }

    /// <summary>VIRTUAL_HOST_MULTIPORTS can expose several hosts from one container.</summary>
    [Test]
    public void MultiportsMapsMultipleHosts()
    {
        const string Yaml =
            """
            a.local:
              "/":
                port: 8080
            b.local:
              "/":
                port: 8081
            """;
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels((DockerLabels.VirtualHostMultiports, Yaml)));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Select(route => route.HostPattern).Should().BeEquivalentTo("a.local", "b.local");
    }

    /// <summary>An invalid VIRTUAL_HOST_MULTIPORTS value is skipped with a warning.</summary>
    [Test]
    public void InvalidMultiportsYamlWarns()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels((DockerLabels.VirtualHostMultiports, "app.local:\n  \"/\": [1, 2")));

        ContainerMapResult result = ContainerMapper.Map([container]);

        result.Contribution.Routes.Should().BeEmpty();
        result.Warnings.Should().Contain(warning => warning.Contains(DockerLabels.VirtualHostMultiports, System.StringComparison.Ordinal));
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
