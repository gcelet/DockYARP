namespace DockYarp.IntegrationTests;

using System.Collections.Generic;

using AwesomeAssertions;

using DockYarp.AdminApi;
using DockYarp.Core.Models;

/// <summary>Tests for <see cref="AdminMapper"/> (the resolve view).</summary>
public sealed class AdminMapperTests
{
    /// <summary>Resolve composes the route, transforms, security, and cluster views (no secrets).</summary>
    [Test]
    public void ResolveComposesTheEffectiveConfig()
    {
        RouteRule route = new()
        {
            HostPattern = "app.local",
            PathPrefix = "/api",
            Priority = 5,
            ClusterId = "app",
            Auth = new BasicAuthCredentials { Username = "admin", Password = "secret" },
            InternalOnly = true,
            ClientCertificate = ClientCertificateRequirement.Required,
            MaxRequestBodySize = 1024,
            Transforms = new RouteTransforms
            {
                PathRemovePrefix = "/api",
                ResponseHeaders = new Dictionary<string, string>(System.StringComparer.Ordinal) { ["X-Frame-Options"] = "DENY" },
            },
            Tls = new HostTlsMetadata { CertificateHost = "app.local", Method = HttpsMethod.Redirect },
        };
        Cluster cluster = new()
        {
            Id = "app",
            LoadBalancingPolicy = LoadBalancingPolicy.RoundRobin,
            Endpoints = [new ClusterEndpoint("c1", "http://10.0.0.1:8080")],
        };

        AdminApiModels.ResolveView view = AdminMapper.Resolve(route, cluster);

        view.Route.Host.Should().Be("app.local");
        view.Route.RequiresAuth.Should().BeTrue();
        view.Route.Tls!.CertificateHost.Should().Be("app.local");
        view.Security.InternalOnly.Should().BeTrue();
        view.Security.ClientCertificate.Should().Be("Required");
        view.Security.MaxRequestBodySize.Should().Be(1024);
        view.Transforms!.PathRemovePrefix.Should().Be("/api");
        view.Transforms.ResponseHeaders.Should().ContainKey("X-Frame-Options");
        view.Cluster!.Id.Should().Be("app");
        view.Cluster.Endpoints.Should().ContainSingle();
    }

    /// <summary>Resolve tolerates a missing target cluster.</summary>
    [Test]
    public void ResolveWithNoClusterYieldsNull()
    {
        RouteRule route = new() { HostPattern = "app.local", ClusterId = "gone" };

        AdminApiModels.ResolveView view = AdminMapper.Resolve(route, cluster: null);

        view.Cluster.Should().BeNull();
        view.Transforms.Should().BeNull();
        view.Security.InternalOnly.Should().BeFalse();
    }
}
