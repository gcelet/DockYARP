namespace DockYarp.IntegrationTests;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.AdminApi;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Integration tests for the real admin certificate and health endpoints.</summary>
public sealed class AdminObservabilityTests
{
    private const string ApiKey = "test-key";

    /// <summary>The certs endpoint returns the certificate inventory.</summary>
    [Test]
    public async Task CertsEndpointReturnsInventory()
    {
        using WebApplicationFactory<Program> factory = Factory(services =>
            services.AddSingleton<ICertificateInventory, FakeCertificateInventory>());
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);

        using HttpResponseMessage response = await client.GetAsync("/api/certs");
        string body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("app.local");
    }

    /// <summary>Health is healthy and reports discovery disabled by default.</summary>
    [Test]
    public async Task HealthIsHealthyWhenDiscoveryDisabled()
    {
        using WebApplicationFactory<Program> factory = Factory(_ => { });
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);

        using HttpResponseMessage response = await client.GetAsync("/api/health");
        string body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("Healthy");
        body.Should().Contain("disabled");
    }

    /// <summary>Health degrades when discovery is enabled but disconnected.</summary>
    [Test]
    public async Task HealthIsDegradedWhenDiscoveryDisconnected()
    {
        using WebApplicationFactory<Program> factory = Factory(services =>
            services.AddSingleton<IDiscoveryHealth>(new FakeDiscoveryHealth { Enabled = true, Connected = false }));
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);

        using HttpResponseMessage response = await client.GetAsync("/api/health");
        string body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("Degraded");
        body.Should().Contain("disconnected");
    }

    private static WebApplicationFactory<Program> Factory(Action<IServiceCollection> configureServices) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AdminApi:ApiKey", ApiKey);
            builder.UseSetting("AdminApi:Surface", "Api");
            builder.UseSetting("AdminApi:Host", "localhost");
            builder.ConfigureTestServices(configureServices);
        });

    private sealed class FakeCertificateInventory : ICertificateInventory
    {
        public IReadOnlyList<AdminApiModels.CertView> List() =>
            [new AdminApiModels.CertView("app.local", "2027-01-01T00:00:00.0000000+00:00")];
    }

    private sealed class FakeDiscoveryHealth : IDiscoveryHealth
    {
        public bool Enabled { get; init; }

        public bool Connected { get; init; }
    }
}
