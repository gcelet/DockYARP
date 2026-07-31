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

    /// <summary>NETWORK_ACCESS=internal marks the route internal-only.</summary>
    [Test]
    public void NetworkAccessInternalMarksInternalOnly()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.NetworkAccess, "internal")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.InternalOnly.Should().BeTrue();
    }

    /// <summary>Without NETWORK_ACCESS the route is not internal-only.</summary>
    [Test]
    public void NoNetworkAccessIsNotInternalOnly()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.InternalOnly.Should().BeFalse();
    }

    /// <summary>A VIRTUAL_DEST destination rewrites the matched prefix: strip VIRTUAL_PATH and prepend the dest.</summary>
    [Test]
    public void VirtualDestRewritesPrefix()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.VirtualPath, "/api"),
                (DockerLabels.VirtualDest, "/v2")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.PathRemovePrefix.Should().Be("/api");
        config.PathAddPrefix.Should().Be("/v2");
    }

    /// <summary>A regex VIRTUAL_PATH ignores VIRTUAL_DEST (a regex location has no fixed prefix to rewrite).</summary>
    [Test]
    public void RegexVirtualPathIgnoresDest()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.VirtualPath, "~^/(app1|alt1)/"),
                (DockerLabels.VirtualDest, "/v2")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.PathPrefix.Should().Be("~^/(app1|alt1)/");
        config.PathRemovePrefix.Should().BeNull();
        config.PathAddPrefix.Should().BeNull();
    }

    /// <summary>A root VIRTUAL_DEST strips the matched prefix without prepending (unchanged behavior).</summary>
    [Test]
    public void RootVirtualDestStripsOnly()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.VirtualPath, "/api"),
                (DockerLabels.VirtualDest, "/")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.PathRemovePrefix.Should().Be("/api");
        config.PathAddPrefix.Should().BeNull();
    }

    /// <summary>Without VIRTUAL_DEST the original path is forwarded (no rewrite).</summary>
    [Test]
    public void NoVirtualDestForwardsOriginalPath()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.VirtualPath, "/api")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.PathRemovePrefix.Should().BeNull();
        config.PathAddPrefix.Should().BeNull();
    }

    /// <summary>A repeated host in VIRTUAL_HOST is de-duplicated.</summary>
    [Test]
    public void DuplicateHostsAreDeduplicated()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local,app.local"),
                (DockerLabels.VirtualPort, "8080")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.Hosts.Should().ContainSingle().Which.Should().Be("app.local");
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

    /// <summary>DOCKYARP_LB maps each supported value (dash/case-insensitive) to its policy.</summary>
    [TestCase("round-robin", LoadBalancingPolicy.RoundRobin)]
    [TestCase("least-requests", LoadBalancingPolicy.LeastRequests)]
    [TestCase("power-of-two-choices", LoadBalancingPolicy.PowerOfTwoChoices)]
    [TestCase("Random", LoadBalancingPolicy.Random)]
    [TestCase("first-alphabetical", LoadBalancingPolicy.FirstAlphabetical)]
    public void LoadBalancingPolicyIsParsed(string label, LoadBalancingPolicy expected)
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.LoadBalancing, label)));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.LoadBalancingPolicy.Should().Be(expected);
        LabelParser.HasUnsupportedLoadBalancing(container.Labels).Should().BeFalse();
    }

    /// <summary>An unrecognized DOCKYARP_LB yields no policy and is flagged unsupported.</summary>
    [Test]
    public void UnknownLoadBalancingIsFlagged()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.LoadBalancing, "ip-hash")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.LoadBalancingPolicy.Should().BeNull();
        LabelParser.HasUnsupportedLoadBalancing(container.Labels).Should().BeTrue();
    }

    /// <summary>CERT_NAME is parsed into the config's shared-certificate name.</summary>
    [Test]
    public void CertNameIsParsed()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.CertName, "shared")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.CertName.Should().Be("shared");
    }

    /// <summary>VIRTUAL_PROTO=grpc selects HTTP transport with HTTP/2-only forwarding.</summary>
    [Test]
    public void GrpcProtoIsHttp2OverHttp()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.VirtualProto, "grpc")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.Scheme.Should().Be(BackendScheme.Http);
        config.Http2.Should().BeTrue();
        LabelParser.HasUnsupportedProto(container.Labels).Should().BeFalse();
    }

    /// <summary>VIRTUAL_PROTO=grpcs selects HTTPS transport with HTTP/2-only forwarding.</summary>
    [Test]
    public void GrpcsProtoIsHttp2OverHttps()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.VirtualProto, "grpcs")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.Scheme.Should().Be(BackendScheme.Https);
        config.Http2.Should().BeTrue();
        LabelParser.HasUnsupportedProto(container.Labels).Should().BeFalse();
    }

    /// <summary>VIRTUAL_PROTO=https selects HTTPS transport without HTTP/2-only forwarding.</summary>
    [Test]
    public void HttpsProtoIsNotHttp2()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.VirtualProto, "https")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.Scheme.Should().Be(BackendScheme.Https);
        config.Http2.Should().BeFalse();
    }
}
