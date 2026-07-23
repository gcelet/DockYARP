namespace DockYarp.E2E.Tests;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Aspire.Hosting;
using Aspire.Hosting.Testing;

/// <summary>Boots the Aspire distributed system once for the whole end-to-end run and exposes the proxy client.</summary>
/// <remarks>
/// The fixture waits only for DockYarp to be healthy (its <c>/metrics</c> endpoint); because discovery is
/// asynchronous, individual scenarios poll their route until it becomes live (see <see cref="E2ETestBase"/>).
/// </remarks>
[SetUpFixture]
public static class AspireAppHostFixture
{
    /// <summary>The admin API key configured on DockYarp by the AppHost (kept in sync on both sides).</summary>
    internal const string ApiKey = "e2e-secret-key";

    private const string ProxyResource = "dockyarp";
    private const string ProxyEndpoint = "http";
    private const string ProxyHttpsEndpoint = "https";
    private const int StartupTimeoutSeconds = 180;

    private static DistributedApplication? application;

    /// <summary>Gets the HTTP client targeting DockYarp's proxy endpoint.</summary>
    internal static HttpClient Proxy { get; private set; } = null!;

    /// <summary>Gets the base address of DockYarp's HTTPS endpoint (TLS clients are built against it).</summary>
    internal static Uri HttpsBaseAddress { get; private set; } = null!;

    /// <summary>Builds the AppHost, starts it, and waits for DockYarp to report healthy.</summary>
    [OneTimeSetUp]
    public static async Task StartAsync()
    {
        // The client CA must exist before DockYarp starts (it is mounted as Tls__ClientCaCertificatePath).
        TlsHarness.PrepareClientCa();

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(StartupTimeoutSeconds));
        CancellationToken token = cts.Token;

        IDistributedApplicationTestingBuilder builder =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.DockYarp_E2E_AppHost>(token);

        application = await builder.BuildAsync(token);
        await application.StartAsync(token);
        await application.ResourceNotifications.WaitForResourceHealthyAsync(ProxyResource, token);

        Proxy = application.CreateHttpClient(ProxyResource, ProxyEndpoint);
        using HttpClient httpsProbe = application.CreateHttpClient(ProxyResource, ProxyHttpsEndpoint);
        HttpsBaseAddress = httpsProbe.BaseAddress!;
    }

    /// <summary>Stops and disposes the distributed application.</summary>
    [OneTimeTearDown]
    public static async Task StopAsync()
    {
        Proxy?.Dispose();
        TlsHarness.Cleanup();
        if (application is not null)
        {
            await application.StopAsync();
            await application.DisposeAsync();
            application = null;
        }
    }
}
