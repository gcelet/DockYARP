namespace DockYarp.IntegrationTests;

using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Core.Interfaces;
using DockYarp.Core.Models;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>End-to-end tests that boot the app and exercise YARP driven by the store.</summary>
public sealed class ProxyIntegrationTests
{
    /// <summary>With no matching route, the proxy returns 404.</summary>
    [Test]
    public async Task UnmatchedHostReturnsNotFound()
    {
        using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>A request for a routed host is proxied to the backend endpoint.</summary>
    [Test]
    public async Task RequestIsProxiedToBackend()
    {
        await using WebApplication backend = BuildBackend();
        await backend.StartAsync();

        try
        {
            using WebApplicationFactory<Program> factory = new();
            using HttpClient client = factory.CreateClient();
            IRouteConfigStore store = factory.Services.GetRequiredService<IRouteConfigStore>();
            string address = backend.Urls.First();

            store.Apply(
                [new RouteRule { HostPattern = "backend.local", ClusterId = "backend" }],
                [new Cluster { Id = "backend", Endpoints = [new ClusterEndpoint("b1", address)] }]);
            client.DefaultRequestHeaders.Host = "backend.local";

            (HttpStatusCode status, string body) = await PollAsync(client);

            status.Should().Be(HttpStatusCode.OK);
            body.Should().Contain("ok");
        }
        finally
        {
            await backend.StopAsync();
        }
    }

    /// <summary>A path-remove-prefix transform strips the prefix before the request reaches the backend.</summary>
    [Test]
    public async Task PathRemovePrefixStripsPrefixBeforeForwarding()
    {
        await using WebApplication backend = BuildEchoBackend();
        await backend.StartAsync();

        try
        {
            using WebApplicationFactory<Program> factory = new();
            using HttpClient client = factory.CreateClient();
            IRouteConfigStore store = factory.Services.GetRequiredService<IRouteConfigStore>();
            string address = backend.Urls.First();

            store.Apply(
                [
                    new RouteRule
                    {
                        HostPattern = "backend.local",
                        PathPrefix = "/api",
                        ClusterId = "backend",
                        Transforms = new RouteTransforms { PathRemovePrefix = "/api" },
                    },
                ],
                [new Cluster { Id = "backend", Endpoints = [new ClusterEndpoint("b1", address)] }]);
            client.DefaultRequestHeaders.Host = "backend.local";

            (HttpStatusCode status, string body) = await PollAsync(client, "/api/orders");

            status.Should().Be(HttpStatusCode.OK);
            body.Should().Be("/orders");
        }
        finally
        {
            await backend.StopAsync();
        }
    }

    /// <summary>An unmatched request returns the configured default status code.</summary>
    [Test]
    public async Task UnmatchedReturnsConfiguredDefaultStatus()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Routing:DefaultResponseStatusCode", "503"));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    /// <summary>A configured default host serves an unknown host by proxying to its backend.</summary>
    [Test]
    public async Task DefaultHostProxiesUnknownHosts()
    {
        await using WebApplication backend = BuildBackend();
        await backend.StartAsync();

        try
        {
            using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseSetting("Routing:DefaultHost", "app.local"));
            using HttpClient client = factory.CreateClient();
            IRouteConfigStore store = factory.Services.GetRequiredService<IRouteConfigStore>();
            string address = backend.Urls.First();

            store.Apply(
                [new RouteRule { HostPattern = "app.local", ClusterId = "app" }],
                [new Cluster { Id = "app", Endpoints = [new ClusterEndpoint("b1", address)] }]);
            client.DefaultRequestHeaders.Host = "unknown.example";

            (HttpStatusCode status, string body) = await PollAsync(client);

            status.Should().Be(HttpStatusCode.OK);
            body.Should().Contain("ok");
        }
        finally
        {
            await backend.StopAsync();
        }
    }

    private static WebApplication BuildBackend()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapFallback(() => "ok");
        return app;
    }

    private static WebApplication BuildEchoBackend()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapFallback(static (HttpContext context) => context.Request.Path.ToString());
        return app;
    }

    private static async Task<(HttpStatusCode Status, string Body)> PollAsync(HttpClient client, string path = "/")
    {
        HttpStatusCode status = HttpStatusCode.ServiceUnavailable;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            using HttpResponseMessage response = await client.GetAsync(path);
            status = response.StatusCode;
            if (status == HttpStatusCode.OK)
            {
                return (status, await response.Content.ReadAsStringAsync());
            }

            await Task.Delay(20);
        }

        return (status, string.Empty);
    }
}
