namespace DockYarp.IntegrationTests;

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using AwesomeAssertions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>Integration tests for the read-only admin dashboard.</summary>
public sealed class DashboardIntegrationTests
{
    private const string ApiKey = "test-key";

    /// <summary>Without a dedicated admin host, the dashboard is reachable on any host.</summary>
    [Test]
    public async Task Dashboard_ServesOnAllHostsWhenAdminHostUnset()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>With a dedicated admin host, the dashboard responds on that host.</summary>
    [Test]
    public async Task Dashboard_ServesOnTheAdminHost()
    {
        using WebApplicationFactory<Program> factory = CreateFactoryWithAdminHost("admin.local");
        using HttpClient client = factory.CreateClient();

        using HttpRequestMessage request = new(HttpMethod.Get, "/dashboard") { Headers = { Host = "admin.local" } };
        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>With a dedicated admin host, the dashboard does not shadow another host's own path.</summary>
    [Test]
    public async Task Dashboard_OtherHostIsNotShadowed()
    {
        using WebApplicationFactory<Program> factory = CreateFactoryWithAdminHost("admin.local");
        using HttpClient client = factory.CreateClient();

        using HttpRequestMessage request = new(HttpMethod.Get, "/dashboard") { Headers = { Host = "other.local" } };
        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    /// <summary>The rendered dashboard never contains the admin API key — it never calls the JSON API.</summary>
    [Test]
    public async Task Dashboard_ResponseDoesNotContainApiKey()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/dashboard");
        string body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain(ApiKey);
    }

    /// <summary>With <c>AdminApi:DashboardEnabled</c> set to <c>false</c>, the dashboard route is absent.</summary>
    [Test]
    public async Task Dashboard_DisabledViaConfig_IsNotServed()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("AdminApi:ApiKey", ApiKey);
                builder.UseSetting("AdminApi:DashboardEnabled", "false");
            });
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/dashboard");

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("AdminApi:ApiKey", ApiKey));

    private static WebApplicationFactory<Program> CreateFactoryWithAdminHost(string adminHost) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("AdminApi:ApiKey", ApiKey);
                builder.UseSetting("AdminApi:Host", adminHost);
            });
}
