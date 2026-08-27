namespace DockYarp.IntegrationTests;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
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

    /// <summary>The download routes are not mapped at all when <c>AdminApi:AllowCertificateDownload</c> is left
    /// at its default (<see langword="false"/>) — proven with an exporter that *would* match, so a 404 here
    /// means the route itself isn't mapped, not merely that the lookup failed.</summary>
    [Test]
    public async Task CertificateDownloadDefaultsToDisabled()
    {
        using WebApplicationFactory<Program> factory = DashboardFactory(
            allowCertificateDownload: false,
            services => services.AddSingleton<ICertificateExporter>(new FakeCertificateExporter()));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage certificateResponse = await client.GetAsync("/dashboard/certs/app.local/certificate");
        using HttpResponseMessage keyResponse = await client.GetAsync("/dashboard/certs/app.local/private-key");

        certificateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        keyResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Downloading a known host's certificate returns the PEM chain as a file attachment.</summary>
    [Test]
    public async Task DownloadingCertificateReturnsPemChain()
    {
        using WebApplicationFactory<Program> factory = DashboardFactory(
            allowCertificateDownload: true,
            services => services.AddSingleton<ICertificateExporter>(new FakeCertificateExporter()));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/dashboard/certs/app.local/certificate");
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Be(FakeCertificateExporter.CertificatePem);
        response.Content.Headers.ContentDisposition?.FileName.Should().Be("app.local.crt");
    }

    /// <summary>Downloading a known host's private key returns the PEM key as a file attachment.</summary>
    [Test]
    public async Task DownloadingPrivateKeyReturnsPemKey()
    {
        using WebApplicationFactory<Program> factory = DashboardFactory(
            allowCertificateDownload: true,
            services => services.AddSingleton<ICertificateExporter>(new FakeCertificateExporter()));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/dashboard/certs/app.local/private-key");
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Be(FakeCertificateExporter.PrivateKeyPem);
        response.Content.Headers.ContentDisposition?.FileName.Should().Be("app.local.key");
    }

    /// <summary>A host with no stored certificate 404s rather than returning an empty or malformed file.</summary>
    [Test]
    public async Task DownloadingUnknownHostReturnsNotFound()
    {
        using WebApplicationFactory<Program> factory = DashboardFactory(
            allowCertificateDownload: true,
            services => services.AddSingleton<ICertificateExporter>(new FakeCertificateExporter()));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/dashboard/certs/unknown.local/certificate");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>The download routes follow the dashboard's own host isolation: a request for a different host
    /// falls through rather than being handled.</summary>
    [Test]
    public async Task DownloadFollowsHostIsolation()
    {
        using WebApplicationFactory<Program> factory = DashboardFactory(
            allowCertificateDownload: true,
            services => services.AddSingleton<ICertificateExporter>(new FakeCertificateExporter()),
            host: "admin.local");
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = "other.local";

        using HttpResponseMessage response = await client.GetAsync("/dashboard/certs/app.local/certificate");

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    /// <summary>A download succeeds with no admin API key present anywhere in the request — it never goes
    /// through the API-key-protected <c>/api/*</c> surface.</summary>
    [Test]
    public async Task DownloadNeedsNoAdminApiKey()
    {
        using WebApplicationFactory<Program> factory = DashboardFactory(
            allowCertificateDownload: true,
            services => services.AddSingleton<ICertificateExporter>(new FakeCertificateExporter()));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/dashboard/certs/app.local/certificate");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Contains("X-Api-Key").Should().BeFalse();
    }

    /// <summary>No conversion action is available when <c>AllowCertificateConversion</c> is left at its
    /// default (<see langword="false"/>): the "Convert to PEM" form doesn't render, even for a host the fake
    /// converter reports as PFX-backed.</summary>
    [Test]
    public async Task CertificateConversionDefaultsToDisabled()
    {
        FakeCertificateConverter converter = new();
        using WebApplicationFactory<Program> factory = ConversionFactory(
            allowCertificateConversion: false,
            services =>
            {
                services.AddSingleton<ICertificateInventory>(new FakeCertificateInventory());
                services.AddSingleton<ICertificateConverter>(converter);
            });
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/dashboard");
        string html = await response.Content.ReadAsStringAsync();

        html.Should().NotContain("Convert to PEM");
        converter.ConvertedHosts.Should().BeEmpty();
    }

    /// <summary>Enabled: submitting the convert form (with its real anti-forgery token) for a PFX-backed host
    /// invokes the converter for that host.</summary>
    [Test]
    public async Task ConvertingCertificateInvokesConverter()
    {
        FakeCertificateConverter converter = new();
        using WebApplicationFactory<Program> factory = ConversionFactory(
            allowCertificateConversion: true,
            services =>
            {
                services.AddSingleton<ICertificateInventory>(new FakeCertificateInventory());
                services.AddSingleton<ICertificateConverter>(converter);
            });
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        string token = await GetAntiForgeryTokenAsync(client, "/dashboard");

        using HttpResponseMessage response = await client.PostAsync(
            "/dashboard/certs/app.local/convert",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("__RequestVerificationToken", token)]));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "post-redirect-get back to /dashboard");
        converter.ConvertedHosts.Should().ContainSingle().Which.Should().Be("app.local");
    }

    /// <summary>The conversion action actually enforces anti-forgery — a request without a valid token is
    /// rejected, not silently honored, proving the CSRF protection is genuinely active rather than assumed.</summary>
    [Test]
    public async Task ConvertingWithoutAntiForgeryTokenIsRejected()
    {
        FakeCertificateConverter converter = new();
        using WebApplicationFactory<Program> factory = ConversionFactory(
            allowCertificateConversion: true,
            services =>
            {
                services.AddSingleton<ICertificateInventory>(new FakeCertificateInventory());
                services.AddSingleton<ICertificateConverter>(converter);
            });
        using HttpClient client = factory.CreateClient();
        await GetAntiForgeryTokenAsync(client, "/dashboard"); // establishes the anti-forgery cookie, token discarded

        using HttpResponseMessage response =
            await client.PostAsync("/dashboard/certs/app.local/convert", new FormUrlEncodedContent([]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        converter.ConvertedHosts.Should().BeEmpty();
    }

    /// <summary>No re-encryption action is available when no private-key encryption passphrase is configured,
    /// even with <c>AllowCertificateConversion</c> enabled — the two settings are independent gates and both
    /// must be satisfied. Also rejected server-side (defense in depth) even when posted with a genuinely valid
    /// anti-forgery token, proving the gate isn't just a hidden button.</summary>
    [Test]
    public async Task KeyReencryptionAbsentAndRejectedWhenPassphraseNotConfigured()
    {
        FakeCertificateConverter converter = new() { PrivateKeyEncryptionConfigured = false };
        using WebApplicationFactory<Program> factory = ConversionFactory(
            allowCertificateConversion: true,
            services =>
            {
                services.AddSingleton<ICertificateInventory>(new FakeCertificateInventory());
                services.AddSingleton<ICertificateConverter>(converter);
            });
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        string token = await GetAntiForgeryTokenAsync(client, "/dashboard");
        string html = await (await client.GetAsync("/dashboard")).Content.ReadAsStringAsync();

        html.Should().NotContain("Re-encrypt key");

        using HttpResponseMessage response = await client.PostAsync(
            "/dashboard/certs/app.local/reencrypt",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("__RequestVerificationToken", token)]));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "the handler no-ops rather than erroring, matching OnPostConvert's own gating shape");
        converter.ReencryptedHosts.Should().BeEmpty();
    }

    /// <summary>Enabled: submitting the re-encrypt form (with its real anti-forgery token) invokes the converter
    /// for that host — proving it isn't restricted to PFX-backed hosts the way <c>ConvertToPem</c>'s own action
    /// is, since the fake converter reports <c>app.local</c> as PFX-backed and a second, plain-key host both work.</summary>
    [TestCase("app.local")]
    [TestCase("plain.local")]
    public async Task ReencryptingKeyInvokesConverter(string host)
    {
        FakeCertificateConverter converter = new() { PrivateKeyEncryptionConfigured = true };
        using WebApplicationFactory<Program> factory = ConversionFactory(
            allowCertificateConversion: true,
            services =>
            {
                services.AddSingleton<ICertificateInventory>(new FakeCertificateInventory());
                services.AddSingleton<ICertificateConverter>(converter);
            });
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        string token = await GetAntiForgeryTokenAsync(client, "/dashboard");

        using HttpResponseMessage response = await client.PostAsync(
            $"/dashboard/certs/{host}/reencrypt",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("__RequestVerificationToken", token)]));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "post-redirect-get back to /dashboard");
        converter.ReencryptedHosts.Should().ContainSingle().Which.Should().Be(host);
    }

    /// <summary>The "needs re-encryption" badge renders only for a host the converter reports as still needing
    /// it, not for every row the re-encryption action itself renders for.</summary>
    [Test]
    public async Task NeedsReencryptionBadgeReflectsPerHostState()
    {
        FakeCertificateConverter converter = new()
        {
            PrivateKeyEncryptionConfigured = true,
            HostsRequiringReencryption = ["app.local"],
        };
        using WebApplicationFactory<Program> factory = ConversionFactory(
            allowCertificateConversion: true,
            services =>
            {
                services.AddSingleton<ICertificateInventory>(new FakeCertificateInventory());
                services.AddSingleton<ICertificateConverter>(converter);
            });
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/dashboard");
        string html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("needs re-encryption");
        html.Should().Contain("Re-encrypt key");
    }

    /// <summary>The re-encryption action actually enforces anti-forgery — a request without a valid token is
    /// rejected, not silently honored (mirrors <see cref="ConvertingWithoutAntiForgeryTokenIsRejected"/>).</summary>
    [Test]
    public async Task ReencryptingWithoutAntiForgeryTokenIsRejected()
    {
        FakeCertificateConverter converter = new() { PrivateKeyEncryptionConfigured = true };
        using WebApplicationFactory<Program> factory = ConversionFactory(
            allowCertificateConversion: true,
            services =>
            {
                services.AddSingleton<ICertificateInventory>(new FakeCertificateInventory());
                services.AddSingleton<ICertificateConverter>(converter);
            });
        using HttpClient client = factory.CreateClient();
        await GetAntiForgeryTokenAsync(client, "/dashboard"); // establishes the anti-forgery cookie, token discarded

        using HttpResponseMessage response =
            await client.PostAsync("/dashboard/certs/app.local/reencrypt", new FormUrlEncodedContent([]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        converter.ReencryptedHosts.Should().BeEmpty();
    }

    /// <summary>No revoke action is available when <c>AllowCertificateRevocation</c> is left at its default
    /// (<see langword="false"/>), mirroring <see cref="CertificateConversionDefaultsToDisabled"/> — its own
    /// independent gate, not tied to <c>AllowCertificateConversion</c>.</summary>
    [Test]
    public async Task CertificateRevocationDefaultsToDisabled()
    {
        FakeCertificateRevoker revoker = new();
        using WebApplicationFactory<Program> factory = RevocationFactory(
            allowCertificateRevocation: false,
            services =>
            {
                services.AddSingleton<ICertificateInventory>(new FakeCertificateInventory());
                services.AddSingleton<ICertificateRevoker>(revoker);
            });
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/dashboard");
        string html = await response.Content.ReadAsStringAsync();

        html.Should().NotContain("Revoke");
        revoker.RevokedHosts.Should().BeEmpty();
    }

    /// <summary>Enabled: submitting the revoke form (with its real anti-forgery token) invokes the revoker for
    /// that host.</summary>
    [Test]
    public async Task RevokingCertificateInvokesRevoker()
    {
        FakeCertificateRevoker revoker = new();
        using WebApplicationFactory<Program> factory = RevocationFactory(
            allowCertificateRevocation: true,
            services =>
            {
                services.AddSingleton<ICertificateInventory>(new FakeCertificateInventory());
                services.AddSingleton<ICertificateRevoker>(revoker);
            });
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        string token = await GetAntiForgeryTokenAsync(client, "/dashboard");

        using HttpResponseMessage response = await client.PostAsync(
            "/dashboard/certs/app.local/revoke",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("__RequestVerificationToken", token)]));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "post-redirect-get back to /dashboard");
        revoker.RevokedHosts.Should().ContainSingle().Which.Should().Be("app.local");
    }

    /// <summary>The revoke action actually enforces anti-forgery — a request without a valid token is
    /// rejected, not silently honored (mirrors <see cref="ConvertingWithoutAntiForgeryTokenIsRejected"/>).</summary>
    [Test]
    public async Task RevokingWithoutAntiForgeryTokenIsRejected()
    {
        FakeCertificateRevoker revoker = new();
        using WebApplicationFactory<Program> factory = RevocationFactory(
            allowCertificateRevocation: true,
            services =>
            {
                services.AddSingleton<ICertificateInventory>(new FakeCertificateInventory());
                services.AddSingleton<ICertificateRevoker>(revoker);
            });
        using HttpClient client = factory.CreateClient();
        await GetAntiForgeryTokenAsync(client, "/dashboard"); // establishes the anti-forgery cookie, token discarded

        using HttpResponseMessage response =
            await client.PostAsync("/dashboard/certs/app.local/revoke", new FormUrlEncodedContent([]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        revoker.RevokedHosts.Should().BeEmpty();
    }

    private static async Task<string> GetAntiForgeryTokenAsync(HttpClient client, string path)
    {
        using HttpResponseMessage response = await client.GetAsync(path);
        string html = await response.Content.ReadAsStringAsync();
        Match match = Regex.Match(html, @"name=""__RequestVerificationToken""[^>]*?value=""([^""]+)""");
        match.Success.Should().BeTrue("the conversion form must render its anti-forgery token");
        return match.Groups[1].Value;
    }

    private static WebApplicationFactory<Program> ConversionFactory(
        bool allowCertificateConversion, Action<IServiceCollection> configureServices) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AdminApi:ApiKey", ApiKey);
            builder.UseSetting("AdminApi:Surface", "ApiAndDashboard");
            builder.UseSetting("AdminApi:Host", "localhost");
            builder.UseSetting("AdminApi:AllowCertificateConversion", allowCertificateConversion.ToString());
            builder.ConfigureTestServices(configureServices);
        });

    private static WebApplicationFactory<Program> RevocationFactory(
        bool allowCertificateRevocation, Action<IServiceCollection> configureServices) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AdminApi:ApiKey", ApiKey);
            builder.UseSetting("AdminApi:Surface", "ApiAndDashboard");
            builder.UseSetting("AdminApi:Host", "localhost");
            builder.UseSetting("AdminApi:AllowCertificateRevocation", allowCertificateRevocation.ToString());
            builder.ConfigureTestServices(configureServices);
        });

    private static WebApplicationFactory<Program> Factory(Action<IServiceCollection> configureServices) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AdminApi:ApiKey", ApiKey);
            builder.UseSetting("AdminApi:Surface", "Api");
            builder.UseSetting("AdminApi:Host", "localhost");
            builder.ConfigureTestServices(configureServices);
        });

    private static WebApplicationFactory<Program> DashboardFactory(
        bool allowCertificateDownload, Action<IServiceCollection> configureServices, string host = "localhost") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AdminApi:ApiKey", ApiKey);
            builder.UseSetting("AdminApi:Surface", "ApiAndDashboard");
            builder.UseSetting("AdminApi:Host", host);
            builder.UseSetting("AdminApi:AllowCertificateDownload", allowCertificateDownload.ToString());
            builder.ConfigureTestServices(configureServices);
        });

    private sealed class FakeCertificateExporter : ICertificateExporter
    {
        public const string CertificatePem = "-----BEGIN CERTIFICATE-----\nFAKE-CERT\n-----END CERTIFICATE-----\n";
        public const string PrivateKeyPem = "-----BEGIN PRIVATE KEY-----\nFAKE-KEY\n-----END PRIVATE KEY-----\n";

        public CertificateExport? Export(string host) =>
            host == "app.local" ? new CertificateExport(CertificatePem, PrivateKeyPem) : null;
    }

    private sealed class FakeCertificateInventory : ICertificateInventory
    {
        public IReadOnlyList<AdminApiModels.CertView> List() =>
            [new AdminApiModels.CertView("app.local", "2027-01-01T00:00:00.0000000+00:00")];
    }

    private sealed class FakeCertificateConverter : ICertificateConverter
    {
        public List<string> ConvertedHosts { get; } = [];

        public List<string> ReencryptedHosts { get; } = [];

        public bool PrivateKeyEncryptionConfigured { get; init; }

        public HashSet<string> HostsRequiringReencryption { get; init; } = [];

        public bool IsPfxBacked(string host) => host == "app.local";

        public bool RequiresKeyReencryption(string host) => HostsRequiringReencryption.Contains(host);

        public bool ConvertToPem(string host)
        {
            ConvertedHosts.Add(host);
            return true;
        }

        public bool ReencryptPrivateKey(string host)
        {
            ReencryptedHosts.Add(host);
            return true;
        }
    }

    private sealed class FakeCertificateRevoker : ICertificateRevoker
    {
        public List<string> RevokedHosts { get; } = [];

        public Task<bool> RevokeCertificateAsync(string host, CancellationToken cancellationToken)
        {
            RevokedHosts.Add(host);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeDiscoveryHealth : IDiscoveryHealth
    {
        public bool Enabled { get; init; }

        public bool Connected { get; init; }
    }
}
