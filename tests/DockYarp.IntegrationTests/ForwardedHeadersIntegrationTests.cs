namespace DockYarp.IntegrationTests;

using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Core.Interfaces;
using DockYarp.Core.Models;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies that forwarded headers reach the backend through the proxy.</summary>
public sealed class ForwardedHeadersIntegrationTests
{
    /// <summary>The backend receives the forwarded scheme and the original host.</summary>
    [Test]
    public async Task ForwardedHeadersReachBackend()
    {
        await using WebApplication backend = BuildEchoBackend();
        await backend.StartAsync();

        try
        {
            using WebApplicationFactory<Program> factory = new();
            using HttpClient client = factory.CreateClient();
            IRouteConfigStore store = factory.Services.GetRequiredService<IRouteConfigStore>();
            store.Apply(
                [new RouteRule { HostPattern = "backend.local", ClusterId = "backend" }],
                [new Cluster { Id = "backend", Endpoints = [new ClusterEndpoint("b1", backend.Urls.First())] }]);
            client.DefaultRequestHeaders.Host = "backend.local";

            string body = await PollForBodyAsync(client);

            body.Should().Contain("proto=http");
            body.Should().Contain("host=backend.local");
            body.Should().Contain("xfhost=backend.local");
        }
        finally
        {
            await backend.StopAsync();
        }
    }

    /// <summary>The backend receives <c>X-Forwarded-Ssl</c> and <c>X-Original-URI</c> reflecting scheme + original URI.</summary>
    [Test]
    public async Task SslAndOriginalUriHeadersReachBackend()
    {
        await using WebApplication backend = BuildEchoBackend();
        await backend.StartAsync();

        try
        {
            using WebApplicationFactory<Program> factory = new();
            using HttpClient client = factory.CreateClient();
            IRouteConfigStore store = factory.Services.GetRequiredService<IRouteConfigStore>();
            store.Apply(
                [new RouteRule { HostPattern = "backend.local", ClusterId = "backend" }],
                [new Cluster { Id = "backend", Endpoints = [new ClusterEndpoint("b1", backend.Urls.First())] }]);
            client.DefaultRequestHeaders.Host = "backend.local";

            string body = await PollForBodyAsync(client, "/orig/path?q=1");

            body.Should().Contain("ssl=off"); // the test client reaches the proxy over HTTP
            body.Should().Contain("origuri=/orig/path?q=1");
        }
        finally
        {
            await backend.StopAsync();
        }
    }

    /// <summary>A client-supplied <c>Proxy</c> header is stripped and never reaches the backend (httpoxy).</summary>
    [Test]
    public async Task ProxyHeaderIsStripped()
    {
        await using WebApplication backend = BuildEchoBackend();
        await backend.StartAsync();

        try
        {
            using WebApplicationFactory<Program> factory = new();
            using HttpClient client = factory.CreateClient();
            IRouteConfigStore store = factory.Services.GetRequiredService<IRouteConfigStore>();
            store.Apply(
                [new RouteRule { HostPattern = "backend.local", ClusterId = "backend" }],
                [new Cluster { Id = "backend", Endpoints = [new ClusterEndpoint("b1", backend.Urls.First())] }]);
            client.DefaultRequestHeaders.Host = "backend.local";
            client.DefaultRequestHeaders.TryAddWithoutValidation("Proxy", "http://attacker.example");

            string body = await PollForBodyAsync(client);

            body.Should().NotContain("attacker");
            body.Should().EndWith("proxy=");
        }
        finally
        {
            await backend.StopAsync();
        }
    }

    private static WebApplication BuildEchoBackend()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapFallback((HttpContext context) => Results.Text(
            $"proto={context.Request.Headers["X-Forwarded-Proto"]};"
            + $"host={context.Request.Headers.Host};"
            + $"xfhost={context.Request.Headers["X-Forwarded-Host"]};"
            + $"ssl={context.Request.Headers["X-Forwarded-Ssl"]};"
            + $"origuri={context.Request.Headers["X-Original-URI"]};"
            + $"proxy={context.Request.Headers["Proxy"]}"));
        return app;
    }

    private static async Task<string> PollForBodyAsync(HttpClient client, string path = "/")
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            using HttpResponseMessage response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return await response.Content.ReadAsStringAsync();
            }

            await Task.Delay(20);
        }

        return string.Empty;
    }
}
