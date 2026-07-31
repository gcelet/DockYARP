namespace DockYarp.IntegrationTests;

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Core.Interfaces;
using DockYarp.Core.Models;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Integration tests for the admin API and metrics endpoint.</summary>
public sealed class AdminApiIntegrationTests
{
    private const string ApiKey = "test-key";

    /// <summary>The admin API rejects requests without a valid API key.</summary>
    [Test]
    public async Task RoutesRequireApiKey()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/routes");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>With a valid key, routes are returned without exposing the auth password.</summary>
    [Test]
    public async Task RoutesWithKeyAreSanitized()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        IRouteConfigStore store = factory.Services.GetRequiredService<IRouteConfigStore>();
        store.Apply(
            [
                new RouteRule
                {
                    HostPattern = "app.local",
                    ClusterId = "app",
                    Auth = new BasicAuthCredentials { Username = "admin", Password = "secret" },
                },
            ],
            [new Cluster { Id = "app", Endpoints = [new ClusterEndpoint("c1", "http://10.0.0.1:8080")] }]);
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);

        using HttpResponseMessage response = await client.GetAsync("/api/routes");
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("app.local");
        body.Should().Contain("requiresAuth");
        body.Should().NotContain("secret");
    }

    /// <summary>The health endpoint reports a status with a valid key.</summary>
    [Test]
    public async Task HealthReturnsStatus()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);

        using HttpResponseMessage response = await client.GetAsync("/api/health");
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("Healthy");
    }

    /// <summary>Resolve returns the effective config for a matching host/path with a valid key.</summary>
    [Test]
    public async Task ResolveReturnsEffectiveConfig()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        IRouteConfigStore store = factory.Services.GetRequiredService<IRouteConfigStore>();
        store.Apply(
            [new RouteRule { HostPattern = "app.local", PathPrefix = "/api", ClusterId = "app" }],
            [new Cluster { Id = "app", Endpoints = [new ClusterEndpoint("c1", "http://10.0.0.1:8080")] }]);
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);

        using HttpResponseMessage response = await client.GetAsync("/api/resolve?host=app.local&path=/api/orders");
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("app.local");
        body.Should().Contain("app");
    }

    /// <summary>Resolve for a host that matches no route returns 404.</summary>
    [Test]
    public async Task ResolveUnknownHostIs404()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);

        using HttpResponseMessage response = await client.GetAsync("/api/resolve?host=nope.local&path=/");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Resolve without an API key is rejected.</summary>
    [Test]
    public async Task ResolveRequiresApiKey()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/resolve?host=app.local");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>The metrics endpoint is scrapable without an API key.</summary>
    [Test]
    public async Task MetricsAreScrapable()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/metrics");
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("dockyarp");
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("AdminApi:ApiKey", ApiKey));
}
