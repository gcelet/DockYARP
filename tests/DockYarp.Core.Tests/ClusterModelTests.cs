namespace DockYarp.Core.Tests;

using System.Collections.Immutable;

using AwesomeAssertions;

using DockYarp.Core.Models;

/// <summary>Tests for the routing domain model.</summary>
public sealed class ClusterModelTests
{
    /// <summary>A route exposes the host-to-cluster mapping it was built with.</summary>
    [Test]
    public void RouteMapsHostToCluster()
    {
        RouteRule route = new() { HostPattern = "app.local", ClusterId = "app" };

        route.HostPattern.Should().Be("app.local");
        route.ClusterId.Should().Be("app");
    }

    /// <summary>A cluster exposes exactly the endpoints it was built with.</summary>
    [Test]
    public void ClusterExposesItsEndpoints()
    {
        Cluster cluster = new()
        {
            Id = "app",
            Endpoints =
            [
                new ClusterEndpoint("c1", "http://10.0.0.1:8080"),
                new ClusterEndpoint("c2", "http://10.0.0.2:8080"),
            ],
        };

        cluster.Endpoints.Should().HaveCount(2);
        cluster.Endpoints.Select(endpoint => endpoint.Address)
            .Should().Contain(["http://10.0.0.1:8080", "http://10.0.0.2:8080"]);
    }

    /// <summary>Adding an endpoint yields a cluster containing both the original and the new one.</summary>
    [Test]
    public void AddingEndpointKeepsBoth()
    {
        Cluster original = new()
        {
            Id = "app",
            Endpoints = [new ClusterEndpoint("c1", "http://10.0.0.1:8080")],
        };

        Cluster updated = original with { Endpoints = original.Endpoints.Add(new ClusterEndpoint("c2", "http://10.0.0.2:8080")) };

        updated.Endpoints.Should().HaveCount(2);
    }

    /// <summary>A route exposes its Basic Auth credentials when configured, and none otherwise.</summary>
    [Test]
    public void RouteExposesAuthCredentials()
    {
        RouteRule protectedRoute = new()
        {
            HostPattern = "app.local",
            ClusterId = "app",
            Auth = new BasicAuthCredentials { Username = "admin", Password = "secret", Realm = "app" },
        };
        RouteRule openRoute = new() { HostPattern = "open.local", ClusterId = "open" };

        protectedRoute.Auth.Should().NotBeNull();
        protectedRoute.Auth!.Username.Should().Be("admin");
        openRoute.Auth.Should().BeNull();
    }

    /// <summary>TLS metadata on a route exposes the certificate host and contact email.</summary>
    [Test]
    public void RouteExposesTlsMetadata()
    {
        RouteRule route = new()
        {
            HostPattern = "app.local",
            ClusterId = "app",
            Tls = new HostTlsMetadata { CertificateHost = "app.local", ContactEmail = "admin@example.com" },
        };

        route.Tls.Should().NotBeNull();
        route.Tls!.CertificateHost.Should().Be("app.local");
        route.Tls.ContactEmail.Should().Be("admin@example.com");
    }
}
