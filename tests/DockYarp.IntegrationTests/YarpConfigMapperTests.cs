namespace DockYarp.IntegrationTests;

using System.Linq;

using AwesomeAssertions;

using DockYarp.App.ReverseProxy;
using DockYarp.Core.Models;
using DockYarp.Security;

using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.SessionAffinity;

/// <summary>Tests for <see cref="YarpConfigMapper"/>.</summary>
public sealed class YarpConfigMapperTests
{
    /// <summary>A route maps to a YARP route matching the host and target cluster.</summary>
    [Test]
    public void RouteMapsToYarpRoute()
    {
        RouteConfigSnapshot snapshot = new(
            [new RouteRule { HostPattern = "app.local", PathPrefix = "/api", ClusterId = "app" }],
            [Cluster("app", LoadBalancingPolicy.RoundRobin)],
            1);

        System.Collections.Generic.IReadOnlyList<RouteConfig> routes = YarpConfigMapper.Map(snapshot).Routes;

        RouteConfig route = routes.Single();
        route.ClusterId.Should().Be("app");
        route.Match.Hosts.Should().Contain("app.local");
        route.Match.Path.Should().Be("/api/{**catch-all}");
    }

    /// <summary>A cluster maps to a YARP cluster with its endpoints and load-balancing policy.</summary>
    [Test]
    public void ClusterMapsWithEndpointsAndPolicy()
    {
        RouteConfigSnapshot snapshot = new(
            [new RouteRule { HostPattern = "app.local", ClusterId = "app" }],
            [Cluster("app", LoadBalancingPolicy.LeastRequests)],
            1);

        System.Collections.Generic.IReadOnlyList<ClusterConfig> clusters = YarpConfigMapper.Map(snapshot).Clusters;

        ClusterConfig cluster = clusters.Single();
        cluster.LoadBalancingPolicy.Should().Be("LeastRequests");
        cluster.Destinations.Should().HaveCount(2);
    }

    /// <summary>Each load-balancing policy maps to YARP's matching policy name.</summary>
    [TestCase(LoadBalancingPolicy.RoundRobin, "RoundRobin")]
    [TestCase(LoadBalancingPolicy.LeastRequests, "LeastRequests")]
    [TestCase(LoadBalancingPolicy.PowerOfTwoChoices, "PowerOfTwoChoices")]
    [TestCase(LoadBalancingPolicy.Random, "Random")]
    [TestCase(LoadBalancingPolicy.FirstAlphabetical, "FirstAlphabetical")]
    public void PolicyMapsToYarpName(LoadBalancingPolicy policy, string expected)
    {
        RouteConfigSnapshot snapshot = new(
            [new RouteRule { HostPattern = "app.local", ClusterId = "app" }],
            [Cluster("app", policy)],
            1);

        System.Collections.Generic.IReadOnlyList<ClusterConfig> clusters = YarpConfigMapper.Map(snapshot).Clusters;

        clusters.Single().LoadBalancingPolicy.Should().Be(expected);
    }

    /// <summary>A cluster health-check configuration maps to YARP active health checks.</summary>
    [Test]
    public void HealthCheckMapsToYarp()
    {
        Cluster withHealth = Cluster("app", LoadBalancingPolicy.RoundRobin) with
        {
            HealthCheck = new DockYarp.Core.Models.HealthCheckConfig
            {
                ActiveEnabled = true,
                Path = "/health",
                Interval = System.TimeSpan.FromSeconds(10),
            },
        };
        RouteConfigSnapshot snapshot = new(
            [new RouteRule { HostPattern = "app.local", ClusterId = "app" }],
            [withHealth],
            1);

        System.Collections.Generic.IReadOnlyList<ClusterConfig> clusters = YarpConfigMapper.Map(snapshot).Clusters;

        clusters.Single().HealthCheck.Should().NotBeNull();
        clusters.Single().HealthCheck!.Active!.Path.Should().Be("/health");
    }

    /// <summary>A route carrying a path-remove-prefix transform maps to a YARP transform entry.</summary>
    [Test]
    public void PathRemovePrefixTransformIsMapped()
    {
        RouteConfigSnapshot snapshot = new(
            [
                new RouteRule
                {
                    HostPattern = "app.local",
                    PathPrefix = "/api",
                    ClusterId = "app",
                    Transforms = new RouteTransforms { PathRemovePrefix = "/api" },
                },
            ],
            [Cluster("app", LoadBalancingPolicy.RoundRobin)],
            1);

        System.Collections.Generic.IReadOnlyList<RouteConfig> routes = YarpConfigMapper.Map(snapshot).Routes;

        routes.Single().Transforms.Should().ContainSingle()
            .Which.Should().Contain(
                new System.Collections.Generic.KeyValuePair<string, string>("PathRemovePrefix", "/api"));
    }

    /// <summary>A destination rewrite maps to a PathRemovePrefix transform followed by a PathPrefix transform.</summary>
    [Test]
    public void PathRewriteMapsToRemoveThenPrefix()
    {
        RouteConfigSnapshot snapshot = new(
            [
                new RouteRule
                {
                    HostPattern = "app.local",
                    PathPrefix = "/api",
                    ClusterId = "app",
                    Transforms = new RouteTransforms { PathRemovePrefix = "/api", PathAddPrefix = "/v2" },
                },
            ],
            [Cluster("app", LoadBalancingPolicy.RoundRobin)],
            1);

        System.Collections.Generic.IReadOnlyList<RouteConfig> routes = YarpConfigMapper.Map(snapshot).Routes;

        System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyDictionary<string, string>> transforms =
            routes.Single().Transforms!;
        transforms.Should().HaveCount(2);
        transforms[0].Should().Contain(
            new System.Collections.Generic.KeyValuePair<string, string>("PathRemovePrefix", "/api"));
        transforms[1].Should().Contain(
            new System.Collections.Generic.KeyValuePair<string, string>("PathPrefix", "/v2"));
    }

    /// <summary>A route with no transform maps to a null transform list (original path forwarded).</summary>
    [Test]
    public void NoTransformProducesNullTransforms()
    {
        RouteConfigSnapshot snapshot = new(
            [new RouteRule { HostPattern = "app.local", ClusterId = "app" }],
            [Cluster("app", LoadBalancingPolicy.RoundRobin)],
            1);

        System.Collections.Generic.IReadOnlyList<RouteConfig> routes = YarpConfigMapper.Map(snapshot).Routes;

        routes.Single().Transforms.Should().BeNull();
    }

    /// <summary>A configured default host adds a lowest-precedence catch-all route to its cluster.</summary>
    [Test]
    public void DefaultHostEmitsCatchAllRoute()
    {
        RouteConfigSnapshot snapshot = new(
            [new RouteRule { HostPattern = "app.local", ClusterId = "app" }],
            [Cluster("app", LoadBalancingPolicy.RoundRobin)],
            1);

        System.Collections.Generic.IReadOnlyList<RouteConfig> routes =
            YarpConfigMapper.Map(snapshot, defaultHost: "app.local", dataProtection: new DockYarp.Security.DataProtectionOptions()).Routes;

        routes.Should().HaveCount(2);
        RouteConfig catchAll = routes.Single(route => route.Match.Hosts is null);
        catchAll.ClusterId.Should().Be("app");
        catchAll.Order.Should().Be(int.MaxValue - 1);
    }

    /// <summary>Without a default host no catch-all route is added.</summary>
    [Test]
    public void NoDefaultHostEmitsNoCatchAll()
    {
        RouteConfigSnapshot snapshot = new(
            [new RouteRule { HostPattern = "app.local", ClusterId = "app" }],
            [Cluster("app", LoadBalancingPolicy.RoundRobin)],
            1);

        System.Collections.Generic.IReadOnlyList<RouteConfig> routes = YarpConfigMapper.Map(snapshot).Routes;

        routes.Should().ContainSingle();
        routes.Single().Match.Hosts.Should().NotBeNull();
    }

    /// <summary>A route priority maps to a negated YARP order; priority 0 leaves the order unset.</summary>
    [Test]
    public void PriorityMapsToYarpOrder()
    {
        RouteConfigSnapshot snapshot = new(
            [
                new RouteRule { HostPattern = "high.local", ClusterId = "app", Priority = 5 },
                new RouteRule { HostPattern = "default.local", ClusterId = "app", Priority = 0 },
            ],
            [Cluster("app", LoadBalancingPolicy.RoundRobin)],
            1);

        System.Collections.Generic.IReadOnlyList<RouteConfig> routes = YarpConfigMapper.Map(snapshot).Routes;

        routes.Single(route => route.RouteId == "high.local|").Order.Should().Be(-5);
        routes.Single(route => route.RouteId == "default.local|").Order.Should().BeNull();
    }

    /// <summary>A cluster request timeout maps to the YARP activity timeout.</summary>
    [Test]
    public void ClusterTimeoutMapsToActivityTimeout()
    {
        Cluster withTimeout = Cluster("app", LoadBalancingPolicy.RoundRobin) with
        {
            RequestTimeout = System.TimeSpan.FromSeconds(30),
        };
        RouteConfigSnapshot snapshot = new(
            [new RouteRule { HostPattern = "app.local", ClusterId = "app" }],
            [withTimeout],
            1);

        System.Collections.Generic.IReadOnlyList<ClusterConfig> clusters = YarpConfigMapper.Map(snapshot).Clusters;

        clusters.Single().HttpRequest!.ActivityTimeout.Should().Be(System.TimeSpan.FromSeconds(30));
    }

    /// <summary>A cluster's max-connections maps to the YARP HttpClient <c>MaxConnectionsPerServer</c>.</summary>
    [Test]
    public void ClusterMaxConnectionsMapsToHttpClient()
    {
        Cluster withLimit = Cluster("app", LoadBalancingPolicy.RoundRobin) with { MaxConnectionsPerServer = 64 };
        RouteConfigSnapshot snapshot = new(
            [new RouteRule { HostPattern = "app.local", ClusterId = "app" }],
            [withLimit],
            1);

        System.Collections.Generic.IReadOnlyList<ClusterConfig> clusters = YarpConfigMapper.Map(snapshot).Clusters;

        clusters.Single().HttpClient!.MaxConnectionsPerServer.Should().Be(64);
    }

    /// <summary>A cluster without a max-connections limit keeps YARP's default pooling (no HttpClient override).</summary>
    [Test]
    public void ClusterWithoutMaxConnectionsHasNoHttpClient()
    {
        RouteConfigSnapshot snapshot = new(
            [new RouteRule { HostPattern = "app.local", ClusterId = "app" }],
            [Cluster("app", LoadBalancingPolicy.RoundRobin)],
            1);

        System.Collections.Generic.IReadOnlyList<ClusterConfig> clusters = YarpConfigMapper.Map(snapshot).Clusters;

        clusters.Single().HttpClient.Should().BeNull();
    }

    /// <summary>An HTTP/2-only (gRPC) cluster forwards over exact HTTP/2.</summary>
    [Test]
    public void Http2OnlyClusterForwardsExactHttp2()
    {
        Cluster grpc = Cluster("app", LoadBalancingPolicy.RoundRobin) with { Http2Only = true };
        RouteConfigSnapshot snapshot = new(
            [new RouteRule { HostPattern = "app.local", ClusterId = "app" }],
            [grpc],
            1);

        System.Collections.Generic.IReadOnlyList<ClusterConfig> clusters = YarpConfigMapper.Map(snapshot).Clusters;

        Yarp.ReverseProxy.Forwarder.ForwarderRequestConfig request = clusters.Single().HttpRequest!;
        request.Version.Should().Be(System.Net.HttpVersion.Version20);
        request.VersionPolicy.Should().Be(System.Net.Http.HttpVersionPolicy.RequestVersionExact);
    }

    /// <summary>Response-header overrides map to YARP ResponseHeader transforms (Set, always).</summary>
    [Test]
    public void ResponseHeadersMapToTransforms()
    {
        RouteConfigSnapshot snapshot = new(
            [
                new RouteRule
                {
                    HostPattern = "app.local",
                    ClusterId = "app",
                    Transforms = new RouteTransforms
                    {
                        ResponseHeaders = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal) { ["X-Frame-Options"] = "DENY" },
                    },
                },
            ],
            [Cluster("app", LoadBalancingPolicy.RoundRobin)],
            1);

        System.Collections.Generic.IReadOnlyList<RouteConfig> routes = YarpConfigMapper.Map(snapshot).Routes;

        routes.Single().Transforms.Should().ContainSingle(t =>
            t.ContainsKey("ResponseHeader") && t["ResponseHeader"] == "X-Frame-Options"
            && t["Set"] == "DENY" && t["When"] == "Always");
    }

    /// <summary>Endpoints sharing an id collapse to one destination instead of throwing.</summary>
    [Test]
    public void DuplicateEndpointsCollapseToOneDestination()
    {
        Cluster cluster = new()
        {
            Id = "app",
            Endpoints =
            [
                new ClusterEndpoint("dup", "http://10.0.0.1:8080"),
                new ClusterEndpoint("dup", "http://10.0.0.2:8080"),
            ],
        };
        RouteConfigSnapshot snapshot = new(
            [new RouteRule { HostPattern = "app.local", ClusterId = "app" }],
            [cluster],
            1);

        System.Collections.Generic.IReadOnlyList<ClusterConfig> clusters = YarpConfigMapper.Map(snapshot).Clusters;

        clusters.Single().Destinations.Should().ContainSingle();
    }

    /// <summary>ClientIpHash affinity is applied regardless of whether Data Protection is configured.</summary>
    [Test]
    public void ClientIpHashAppliesWithoutDataProtection()
    {
        Cluster cluster = Cluster("app", LoadBalancingPolicy.RoundRobin) with { SessionAffinityPolicy = SessionAffinityPolicy.ClientIpHash };
        RouteConfigSnapshot snapshot = new([new RouteRule { HostPattern = "app.local", ClusterId = "app" }], [cluster], 1);

        YarpConfigMapResult result = YarpConfigMapper.Map(snapshot, defaultHost: null, dataProtection: new DataProtectionOptions());

        ClusterConfig mapped = result.Clusters.Single();
        mapped.SessionAffinity.Should().NotBeNull();
        mapped.SessionAffinity!.Policy.Should().Be(ClientIpHashSessionAffinityPolicy.PolicyName);
        result.Diagnostics.Should().BeEmpty();
    }

    /// <summary>Cookie affinity is applied when Data Protection is configured.</summary>
    [Test]
    public void CookieAffinityAppliesWhenDataProtectionConfigured()
    {
        Cluster cluster = Cluster("app", LoadBalancingPolicy.RoundRobin) with { SessionAffinityPolicy = SessionAffinityPolicy.Cookie };
        RouteConfigSnapshot snapshot = new([new RouteRule { HostPattern = "app.local", ClusterId = "app" }], [cluster], 1);

        YarpConfigMapResult result = YarpConfigMapper.Map(
            snapshot, defaultHost: null, dataProtection: new DataProtectionOptions { CertificatePath = "cert.pfx" });

        ClusterConfig mapped = result.Clusters.Single();
        mapped.SessionAffinity.Should().NotBeNull();
        mapped.SessionAffinity!.Policy.Should().Be(SessionAffinityConstants.Policies.Cookie);
        result.Diagnostics.Should().BeEmpty();
    }

    /// <summary>Cookie/CustomHeader affinity degrades to no affinity — not an excluded route — when Data
    /// Protection is not configured, with a diagnostic naming the cluster.</summary>
    [TestCase(SessionAffinityPolicy.Cookie)]
    [TestCase(SessionAffinityPolicy.CustomHeader)]
    public void EncryptedAffinityDegradesWithoutDataProtection(SessionAffinityPolicy policy)
    {
        Cluster cluster = Cluster("app", LoadBalancingPolicy.RoundRobin) with { SessionAffinityPolicy = policy };
        RouteConfigSnapshot snapshot = new([new RouteRule { HostPattern = "app.local", ClusterId = "app" }], [cluster], 1);

        YarpConfigMapResult result = YarpConfigMapper.Map(snapshot, defaultHost: null, dataProtection: new DataProtectionOptions());

        ClusterConfig mapped = result.Clusters.Single();
        mapped.SessionAffinity.Should().BeNull("the route must still be served normally, just without affinity");
        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Contains("app", System.StringComparison.Ordinal));
    }

    /// <summary>A cluster with no affinity is unaffected regardless of Data Protection configuration.</summary>
    [Test]
    public void NoAffinityIsUnaffectedByDataProtection()
    {
        Cluster cluster = Cluster("app", LoadBalancingPolicy.RoundRobin);
        RouteConfigSnapshot snapshot = new([new RouteRule { HostPattern = "app.local", ClusterId = "app" }], [cluster], 1);

        YarpConfigMapResult result = YarpConfigMapper.Map(snapshot, defaultHost: null, dataProtection: new DataProtectionOptions());

        result.Clusters.Single().SessionAffinity.Should().BeNull();
        result.Diagnostics.Should().BeEmpty();
    }

    /// <summary>One cluster's Cookie-affinity degradation does not affect a sibling cluster's own, independent
    /// affinity configuration — proving isolation, not a global effect.</summary>
    [Test]
    public void DegradedClusterDoesNotAffectSiblingCluster()
    {
        Cluster degraded = Cluster("degraded", LoadBalancingPolicy.RoundRobin) with { SessionAffinityPolicy = SessionAffinityPolicy.Cookie };
        Cluster unaffected = Cluster("unaffected", LoadBalancingPolicy.RoundRobin) with { SessionAffinityPolicy = SessionAffinityPolicy.ClientIpHash };
        RouteConfigSnapshot snapshot = new(
            [
                new RouteRule { HostPattern = "degraded.local", ClusterId = "degraded" },
                new RouteRule { HostPattern = "unaffected.local", ClusterId = "unaffected" },
            ],
            [degraded, unaffected],
            1);

        YarpConfigMapResult result = YarpConfigMapper.Map(snapshot, defaultHost: null, dataProtection: new DataProtectionOptions());

        result.Clusters.Single(cluster => cluster.ClusterId == "degraded").SessionAffinity.Should().BeNull();
        result.Clusters.Single(cluster => cluster.ClusterId == "unaffected").SessionAffinity.Should().NotBeNull();
        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Contains("degraded", System.StringComparison.Ordinal));
    }

    private static Cluster Cluster(string id, LoadBalancingPolicy policy) =>
        new()
        {
            Id = id,
            LoadBalancingPolicy = policy,
            Endpoints =
            [
                new ClusterEndpoint("c1", "http://10.0.0.1:8080"),
                new ClusterEndpoint("c2", "http://10.0.0.2:8080"),
            ],
        };
}
