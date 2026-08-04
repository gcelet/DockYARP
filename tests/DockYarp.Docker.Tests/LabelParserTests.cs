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

    /// <summary>EffectiveConfig merges env over labels (env wins), keeping label-only and env-only keys.</summary>
    [Test]
    public void EffectiveConfigMergesEnvOverLabels()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "label.local"),
                (DockerLabels.VirtualPort, "8080")))
            with
        {
            Env = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal)
            {
                [DockerLabels.VirtualHost] = "env.local",
                [DockerLabels.VirtualProto] = "https",
            },
        };

        System.Collections.Generic.IReadOnlyDictionary<string, string> config = LabelParser.EffectiveConfig(container);

        config[DockerLabels.VirtualHost].Should().Be("env.local");   // env wins
        config[DockerLabels.VirtualPort].Should().Be("8080");         // label-only kept
        config[DockerLabels.VirtualProto].Should().Be("https");       // env-only kept
    }

    /// <summary>SSL_POLICY is parsed into the config; an env value wins over a same-named label.</summary>
    [Test]
    public void SslPolicyIsParsedAndEnvWins()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.SslPolicy, "Mozilla-Old")))
            with
        {
            Env = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal)
            {
                [DockerLabels.SslPolicy] = "Mozilla-Modern",
            },
        };

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.SslPolicy.Should().Be("Mozilla-Modern"); // env wins over the label
    }

    /// <summary>SERVER_TOKENS is parsed into the config.</summary>
    [Test]
    public void ServerTokensIsParsed()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.ServerTokens, "off")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.ServerTokens.Should().Be("off");
    }

    /// <summary>With no env vars, EffectiveConfig returns the labels unchanged.</summary>
    [Test]
    public void EffectiveConfigWithoutEnvReturnsLabels()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1", "10.0.0.1", DiscoveryTestData.Labels((DockerLabels.VirtualHost, "app.local")));

        LabelParser.EffectiveConfig(container).Should().BeSameAs(container.Labels);
    }

    /// <summary>Configuration set only as environment variables is parsed and routable.</summary>
    [Test]
    public void ConfigFromEnvironmentIsParsed()
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

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.Hosts.Should().ContainSingle().Which.Should().Be("app.local");
        config.Port.Should().Be(8080);
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

    /// <summary>DOCKYARP_MAX_CONNECTIONS is parsed as a positive connection count.</summary>
    [Test]
    public void MaxConnectionsIsParsed()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.MaxConnections, "64")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.MaxConnectionsPerServer.Should().Be(64);
        LabelParser.HasInvalidMaxConnections(container.Labels).Should().BeFalse();
    }

    /// <summary>A non-positive DOCKYARP_MAX_CONNECTIONS is ignored and flagged invalid.</summary>
    [Test]
    public void InvalidMaxConnectionsIsIgnoredAndFlagged()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.MaxConnections, "0")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.MaxConnectionsPerServer.Should().BeNull();
        LabelParser.HasInvalidMaxConnections(container.Labels).Should().BeTrue();
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

    /// <summary>The nginx-proxy ssl_verify_client label aliases DOCKYARP_CLIENT_CERT with value translation.</summary>
    [Test]
    public void NginxSslVerifyClientAliasMapsToClientCert()
    {
        ParseConfig(DockerLabels.NginxSslVerifyClient, "on").ClientCertificate
            .Should().Be(ClientCertificateRequirement.Required);
        ParseConfig(DockerLabels.NginxSslVerifyClient, "optional").ClientCertificate
            .Should().Be(ClientCertificateRequirement.Optional);
        ParseConfig(DockerLabels.NginxSslVerifyClient, "off").ClientCertificate
            .Should().Be(ClientCertificateRequirement.None);
    }

    /// <summary>The nginx-proxy loadbalance label aliases DOCKYARP_LB (nginx directives + DockYarp names).</summary>
    [Test]
    public void NginxLoadBalanceAliasMapsToPolicy()
    {
        ParseConfig(DockerLabels.NginxLoadBalance, "least_conn;").LoadBalancingPolicy
            .Should().Be(LoadBalancingPolicy.LeastRequests);
        ParseConfig(DockerLabels.NginxLoadBalance, "least-requests").LoadBalancingPolicy
            .Should().Be(LoadBalancingPolicy.LeastRequests);
        ParseConfig(DockerLabels.NginxLoadBalance, "hash $remote_addr;").LoadBalancingPolicy
            .Should().BeNull(); // hashing is session affinity, not a policy
    }

    /// <summary>When both the DockYarp-native key and the namespaced label are set, the native value wins.</summary>
    [Test]
    public void DockYarpNativeKeyWinsOverNamespacedLabel()
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.ClientCert, "required"),
                (DockerLabels.NginxSslVerifyClient, "optional")));

        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();

        config!.ClientCertificate.Should().Be(ClientCertificateRequirement.Required);
    }

    /// <summary>EXTERNAL_HTTPS_PORT is parsed; an invalid value is flagged and ignored.</summary>
    [Test]
    public void ExternalHttpsPortIsParsed()
    {
        ParseConfig(DockerLabels.ExternalHttpsPort, "8443").ExternalHttpsPort.Should().Be(8443);

        ContainerInfo invalid = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (DockerLabels.ExternalHttpsPort, "nope")));

        LabelParser.TryParse(invalid, out ContainerLabelConfig? config, out _).Should().BeTrue();
        config!.ExternalHttpsPort.Should().BeNull();
        LabelParser.HasInvalidExternalHttpsPort(invalid.Labels).Should().BeTrue();
    }

    /// <summary>ENABLE_HTTP_ON_MISSING_CERT / TRUST_DEFAULT_CERT parse as booleans (the latter also via the alias).</summary>
    [Test]
    public void MissingCertOverridesAreParsed()
    {
        ParseConfig(DockerLabels.EnableHttpOnMissingCert, "false").EnableHttpOnMissingCert.Should().BeFalse();
        ParseConfig(DockerLabels.TrustDefaultCert, "false").TrustDefaultCert.Should().BeFalse();
        ParseConfig(DockerLabels.NginxTrustDefaultCert, "false").TrustDefaultCert.Should().BeFalse();
    }

    private static ContainerLabelConfig ParseConfig(string aliasKey, string aliasValue)
    {
        ContainerInfo container = DiscoveryTestData.Container(
            "c1",
            "10.0.0.1",
            DiscoveryTestData.Labels(
                (DockerLabels.VirtualHost, "app.local"),
                (DockerLabels.VirtualPort, "8080"),
                (aliasKey, aliasValue)));
        LabelParser.TryParse(container, out ContainerLabelConfig? config, out _).Should().BeTrue();
        return config!;
    }
}
