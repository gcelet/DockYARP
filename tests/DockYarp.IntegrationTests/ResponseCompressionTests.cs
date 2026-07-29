namespace DockYarp.IntegrationTests;

using System;
using System.Net.Http;
using System.Threading.Tasks;

using AwesomeAssertions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>Integration tests for response compression over the real host pipeline.</summary>
public sealed class ResponseCompressionTests
{
    private const string ApiKey = "test-key";
    private const string EnabledEnvVar = "Compression__Enabled";

    /// <summary>A compressible response is gzip-encoded when the client accepts gzip and compression is on.</summary>
    [Test]
    public async Task CompressibleResponseIsGzipped()
    {
        using WebApplicationFactory<Program> factory = Factory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await GetHealthAsync(client);

        response.Content.Headers.ContentEncoding.Should().Contain("gzip");
    }

    /// <summary>With compression disabled by configuration, the response is not encoded.</summary>
    [Test]
    public async Task CompressionDisabledPassesThrough()
    {
        Environment.SetEnvironmentVariable(EnabledEnvVar, "false");
        try
        {
            using WebApplicationFactory<Program> factory = Factory();
            using HttpClient client = factory.CreateClient();

            using HttpResponseMessage response = await GetHealthAsync(client);

            response.Content.Headers.ContentEncoding.Should().BeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnabledEnvVar, null);
        }
    }

    // GET /api/health (a compressible JSON payload handled by DockYarp's own pipeline, not the metrics exporter).
    private static async Task<HttpResponseMessage> GetHealthAsync(HttpClient client)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/health");
        request.Headers.Add("X-Api-Key", ApiKey);
        request.Headers.AcceptEncoding.ParseAdd("gzip");
        return await client.SendAsync(request);
    }

    private static WebApplicationFactory<Program> Factory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("AdminApi:ApiKey", ApiKey));
}
