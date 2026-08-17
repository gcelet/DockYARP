namespace DockYarp.E2E.Tests;

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.E2E.AppHost;

/// <summary>TLS scenarios: ACME provisioning, self-signed fallback, HTTPS redirect, HSTS, and mutual TLS.</summary>
[Category("EndToEnd")]
public sealed class TlsTests : E2ETestBase
{
    // Provisioning is asynchronous (ACME round-trip against step-ca), so TLS scenarios poll generously.
    private const int TlsPollSeconds = 60;

    // The step-ca CA name (DOCKER_STEPCA_INIT_NAME) appears in the issuer of ACME-issued certificates.
    private const string CaIssuerMarker = "DockYarp E2E";

    /// <summary>A certificate issued by the local ACME authority is served for a <c>LETSENCRYPT_HOST</c>.</summary>
    [Test]
    public async Task AcmeCertificate_IsProvisionedForHost()
    {
        TlsHarness.ServerCertificateHolder capture = new();
        using HttpClient client = TlsHarness.CreateClient(capture);

        // Poll until the served certificate is the ACME-issued one (provisioning is asynchronous); a plain
        // success would accept the self-signed fallback served before the certificate is provisioned.
        using HttpResponseMessage response = await PollAsync(
            client,
            static () => new HttpRequestMessage(HttpMethod.Get, "https://tls.local/"),
            _ => capture.ServerCertificate?.Issuer.Contains(CaIssuerMarker, StringComparison.Ordinal) == true,
            TlsPollSeconds);

        capture.ServerCertificate.Should().NotBeNull();
        capture.ServerCertificate!.Issuer.Should().Contain(CaIssuerMarker);
    }

    /// <summary>The intermediate is actually sent during the handshake for an ACME-issued certificate: a client
    /// trusting only the step-ca root — no intermediate, no system/OS trust store — still builds a complete
    /// chain. This is the regression test for the bare-ServerCertificate bug: pre-fix, SslStream built its own
    /// chain via system-store-dependent logic and never sent the intermediate, so this would fail with a
    /// PartialChain status.</summary>
    [Test]
    public async Task AcmeCertificate_ChainIncludesIntermediate()
    {
        using X509Certificate2 stepCaRoot = X509CertificateLoader.LoadCertificateFromFile(
            Path.Combine(E2EPaths.StepCaDirectory, "certs", "root_ca.crt"));
        TlsHarness.ServerCertificateHolder capture = new();
        using HttpClient client = TlsHarness.CreateClientWithChainValidation(capture, stepCaRoot);

        using HttpResponseMessage response = await PollAsync(
            client,
            static () => new HttpRequestMessage(HttpMethod.Get, "https://tls.local/"),
            _ => capture.ServerCertificate?.Issuer.Contains(CaIssuerMarker, StringComparison.Ordinal) == true,
            TlsPollSeconds);

        capture.ServerCertificate.Should().NotBeNull();
        capture.PolicyErrors.Should().Be(
            SslPolicyErrors.None,
            "the intermediate must be sent in the handshake so a client trusting only the root can build the chain");
    }

    /// <summary>The intermediate is actually sent during the handshake for an operator-provided (mounted PEM,
    /// non-ACME) certificate: a client trusting only the throwaway chain's own CA still builds a complete
    /// chain. Covers the PEM-loading path independently of ACME/CertesAcmeClient.</summary>
    [Test]
    public async Task MountedPemCertificate_ChainIncludesIntermediate()
    {
        TlsHarness.ServerCertificateHolder capture = new();
        using HttpClient client = TlsHarness.CreateClientWithChainValidation(capture, TlsHarness.MountedChainRoot);

        using HttpResponseMessage response = await PollAsync(
            client,
            static () => new HttpRequestMessage(HttpMethod.Get, "https://pem.local/"),
            static message => message.IsSuccessStatusCode,
            TlsPollSeconds);

        response.IsSuccessStatusCode.Should().BeTrue();
        capture.ServerCertificate.Should().NotBeNull();
        capture.PolicyErrors.Should().Be(
            SslPolicyErrors.None,
            "the intermediate must be sent in the handshake so a client trusting only the chain's own CA can build it");
    }

    /// <summary>An unknown host is served the self-signed fallback certificate (not an ACME one).</summary>
    [Test]
    public async Task UnknownHost_UsesSelfSignedFallback()
    {
        TlsHarness.ServerCertificateHolder capture = new();
        using HttpClient client = TlsHarness.CreateClient(capture);

        using HttpResponseMessage response = await PollAsync(
            client,
            static () => new HttpRequestMessage(HttpMethod.Get, "https://untrusted.local/"),
            static message => message.IsSuccessStatusCode,
            TlsPollSeconds);

        response.IsSuccessStatusCode.Should().BeTrue();
        capture.ServerCertificate.Should().NotBeNull();
        capture.ServerCertificate!.Issuer.Should().NotContain(CaIssuerMarker);
    }

    /// <summary>Once a certificate is available, an HTTP request for the host is redirected to HTTPS.</summary>
    [Test]
    public async Task HttpRequest_RedirectsToHttps()
    {
        using HttpClient client = TlsHarness.CreateNonRedirectingHttpClient();

        using HttpResponseMessage response = await PollAsync(
            client,
            static () => Request(HttpMethod.Get, "tls.local", "/"),
            static message => (int)message.StatusCode is >= 300 and < 400,
            TlsPollSeconds);

        response.StatusCode.Should().Be(HttpStatusCode.PermanentRedirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.Scheme.Should().Be("https");
    }

    /// <summary>A host with an <c>HSTS</c> policy returns the Strict-Transport-Security header over HTTPS.</summary>
    [Test]
    public async Task Hsts_HeaderIsPresentOverHttps()
    {
        TlsHarness.ServerCertificateHolder capture = new();
        using HttpClient client = TlsHarness.CreateClient(capture);

        using HttpResponseMessage response = await PollAsync(
            client,
            static () => new HttpRequestMessage(HttpMethod.Get, "https://hsts.local/"),
            static message => message.IsSuccessStatusCode,
            TlsPollSeconds);

        response.Headers.Contains("Strict-Transport-Security").Should().BeTrue();
    }

    /// <summary>A mutual-TLS host rejects a request that presents no client certificate.</summary>
    [Test]
    public async Task MutualTls_RejectsWithoutClientCertificate()
    {
        TlsHarness.ServerCertificateHolder capture = new();
        using HttpClient client = TlsHarness.CreateClient(capture);

        using HttpResponseMessage response = await PollAsync(
            client,
            static () => new HttpRequestMessage(HttpMethod.Get, "https://mtls.local/"),
            static message => message.StatusCode == HttpStatusCode.Forbidden,
            TlsPollSeconds);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The same host accepts a request presenting a certificate chaining to the configured client CA, and
    /// the verified client identity is passed through to the backend.</summary>
    [Test]
    public async Task MutualTls_AcceptsValidClientCertificate()
    {
        TlsHarness.ServerCertificateHolder capture = new();
        using HttpClient client = TlsHarness.CreateMutualTlsClient(capture);

        using HttpResponseMessage response = await PollAsync(
            client,
            static () => new HttpRequestMessage(HttpMethod.Get, "https://mtls.local/"),
            static message => message.IsSuccessStatusCode,
            TlsPollSeconds);

        response.IsSuccessStatusCode.Should().BeTrue();

        // The backend echoes request headers: the verified client cert is forwarded as X-SSL-Client-Verify + S-DN.
        JsonElement echo = await ReadJsonAsync(response);
        JsonElement headers = echo.GetProperty("headers");
        HeaderValue(headers, "X-SSL-Client-Verify").Should().Be("SUCCESS");
        HeaderValue(headers, "X-SSL-Client-S-DN").Should().NotBeEmpty();
    }

    private static string HeaderValue(JsonElement headers, string name)
    {
        foreach (JsonProperty header in headers.EnumerateObject())
        {
            if (string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return header.Value.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
