namespace DockYarp.E2E.Tests;

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>Boots the Aspire distributed system once for the whole end-to-end run and exposes the proxy client.</summary>
/// <remarks>
/// The fixture waits only for DockYarp to be healthy (its <c>/metrics</c> endpoint); because discovery is
/// asynchronous, individual scenarios poll their route until it becomes live (see <see cref="E2ETestBase"/>).
/// Each resource's logs are written to per-resource files under <see cref="LogDirectory"/> so a failure can be
/// diagnosed after the containers are torn down.
/// </remarks>
[SetUpFixture]
public static class AspireAppHostFixture
{
    /// <summary>The admin API key configured on DockYarp by the AppHost (kept in sync on both sides).</summary>
    internal const string ApiKey = "e2e-secret-key";

    private const string ProxyResource = "dockyarp";
    private const string ProxyEndpoint = "http";
    private const string ProxyHttpsEndpoint = "https";
    private const string ProxyProtocolResource = "dockyarp-pp";

    // A generous, deliberately-not-precisely-measured margin, not a minimum derived from local timing: local
    // runs (including under CPU/RAM matched to a GitHub-hosted runner) complete the same startup in well under
    // two minutes, but two real GitHub Actions runs both hit the previous 180s budget exactly, never finishing
    // organically. See openspec/backlog/items/fix-e2e-ci-runner-timeout.md's investigation log.
    private const int StartupTimeoutSeconds = 420;

    private static DistributedApplication? application;

    /// <summary>Gets the HTTP client targeting DockYarp's proxy endpoint.</summary>
    internal static HttpClient Proxy { get; private set; } = null!;

    /// <summary>Gets the base address of DockYarp's HTTPS endpoint (TLS clients are built against it).</summary>
    internal static Uri HttpsBaseAddress { get; private set; } = null!;

    /// <summary>Gets the HTTP edge of the dedicated PROXY-protocol DockYarp instance (raw-socket clients target it).</summary>
    internal static Uri ProxyProtocolBaseAddress { get; private set; } = null!;

    /// <summary>Gets the directory where per-resource logs are captured for post-mortem diagnostics.</summary>
    internal static string LogDirectory { get; private set; } = string.Empty;

    /// <summary>Builds the AppHost, starts it, and waits for DockYarp to report healthy.</summary>
    [OneTimeSetUp]
    public static async Task StartAsync()
    {
        // The client CA and the operator-provided pem.local chain must exist before DockYarp starts: the former
        // is mounted as Tls__ClientCaCertificatePath, the latter is loaded once from the certs directory at
        // startup (FileCertificateStore has no live reload).
        TlsHarness.PrepareClientCa();
        TlsHarness.PrepareMountedChain();

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(StartupTimeoutSeconds));
        CancellationToken token = cts.Token;

        IDistributedApplicationTestingBuilder builder =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.DockYarp_E2E_AppHost>(token);

        // Aspire's testing host redirects each resource's logs to the application's logging pipeline, so a
        // logging provider captures them to durable per-resource files (surviving container teardown).
        string logDirectory = Environment.GetEnvironmentVariable("DOCKYARP_E2E_LOG_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "e2e-logs");
        Directory.CreateDirectory(logDirectory);
        LogDirectory = logDirectory;
        builder.Services.AddLogging(logging => logging
            .AddProvider(new ResourceFileLoggerProvider(logDirectory))
            .AddFilter<ResourceFileLoggerProvider>(category: null, LogLevel.Trace));

        application = await builder.BuildAsync(token);
        await application.StartAsync(token);
        await application.ResourceNotifications.WaitForResourceHealthyAsync(ProxyResource, token);

        // Only safe to widen step-ca's PKI file permissions now: DockYarp itself .WaitForCompletion(caBundle),
        // and ca-bundle polls until step-ca's root_ca.crt/intermediate_ca.crt exist — so the proxy resource
        // reporting healthy transitively guarantees step-ca has finished writing them. Doing this any earlier
        // would race step-ca's own PKI init.
        TlsHarness.MakeStepCaPkiReadable();

        Proxy = application.CreateHttpClient(ProxyResource, ProxyEndpoint);
        using HttpClient httpsProbe = application.CreateHttpClient(ProxyResource, ProxyHttpsEndpoint);
        HttpsBaseAddress = httpsProbe.BaseAddress!;

        // The PROXY-protocol instance has no health gate (a plain probe would be rejected); its endpoint is
        // allocated at start and the proxy-protocol test polls the edge over a raw socket until the route is live.
        using HttpClient proxyProtocolProbe = application.CreateHttpClient(ProxyProtocolResource, ProxyEndpoint);
        ProxyProtocolBaseAddress = proxyProtocolProbe.BaseAddress!;
    }

    /// <summary>Restarts the DockYarp container and waits for it to report healthy again.</summary>
    /// <param name="token">A token to cancel the wait.</param>
    /// <remarks>
    /// Recreates the container against the same volumes (the restart-persistence scenario asserts that persisted
    /// state survives). The suite runs sequentially, and this leaves the shared proxy healthy for later tests.
    /// </remarks>
    internal static async Task RestartProxyAndWaitHealthyAsync(CancellationToken token)
    {
        DistributedApplication app =
            application ?? throw new InvalidOperationException("The application has not been started.");

        ResourceCommandService commands = app.Services.GetRequiredService<ResourceCommandService>();
        ExecuteCommandResult result =
            await commands.ExecuteCommandAsync(ProxyResource, KnownResourceCommands.RestartCommand, token);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Restarting '{ProxyResource}' failed: {result.Message}");
        }

        await app.ResourceNotifications.WaitForResourceHealthyAsync(ProxyResource, token);
    }

    /// <summary>Stops and disposes the distributed application, flushing the captured logs.</summary>
    [OneTimeTearDown]
    public static async Task StopAsync()
    {
        Proxy?.Dispose();
        TlsHarness.Cleanup();
        if (application is not null)
        {
            // DisposeAsync disposes the logger factory, which flushes and closes the per-resource log files.
            await application.StopAsync();
            await application.DisposeAsync();
            application = null;
        }
    }
}
