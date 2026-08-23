namespace DockYarp.E2E.Tests;

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
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

    /// <summary>A DNS-01 host with a wildcard <c>LETSENCRYPT_HOST</c> (<c>*.dns01.example</c>) gets a real
    /// wildcard certificate via RFC 2136 against a throwaway BIND9 authority — proving the hand-rolled DNS
    /// UPDATE/TSIG code, the DNS-01 challenge flow, and SniCertificateSelector's existing wildcard
    /// parent-domain fallback (the cert is stored under <c>dns01.example</c>, matched here to the specific
    /// <c>sub.dns01.example</c> route) all work together for real — no mocking anywhere in this test.</summary>
    [Test]
    public async Task AcmeWildcardCertificate_IsProvisionedViaDns01()
    {
        TlsHarness.ServerCertificateHolder capture = new();
        using HttpClient client = TlsHarness.CreateClient(capture);

        using HttpResponseMessage response = await PollAsync(
            client,
            static () => new HttpRequestMessage(HttpMethod.Get, "https://sub.dns01.example/"),
            _ => capture.ServerCertificate?.Issuer.Contains(CaIssuerMarker, StringComparison.Ordinal) == true,
            TlsPollSeconds);

        capture.ServerCertificate.Should().NotBeNull();
        capture.ServerCertificate!.Issuer.Should().Contain(CaIssuerMarker);
        capture.ServerCertificate!.Subject.Should().Contain(
            "dns01.example", "the wildcard order's CN is *.dns01.example, stored under the parent domain");
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

    /// <summary>An <c>optional</c> mutual-TLS host succeeds with no client certificate presented, and the
    /// backend receives <c>X-SSL-Client-Verify: NONE</c> — the connection is never dropped, unlike a
    /// <c>required</c> host.</summary>
    [Test]
    public async Task MutualTlsOptional_NoCertificateSucceedsAsNone()
    {
        TlsHarness.ServerCertificateHolder capture = new();
        using HttpClient client = TlsHarness.CreateClient(capture);

        using HttpResponseMessage response = await PollAsync(
            client,
            static () => new HttpRequestMessage(HttpMethod.Get, "https://mtls-optional.local/"),
            static message => message.IsSuccessStatusCode,
            TlsPollSeconds);

        response.IsSuccessStatusCode.Should().BeTrue();
        JsonElement echo = await ReadJsonAsync(response);
        HeaderValue(echo.GetProperty("headers"), "X-SSL-Client-Verify").Should().Be("NONE");
    }

    /// <summary>An <c>optional</c> mutual-TLS host succeeds even when the presented client certificate does not
    /// chain to the configured client CA — the real proof this item exists for: a real TLS handshake genuinely
    /// not dropping an untrusted client certificate. The backend receives <c>X-SSL-Client-Verify: FAILED</c>.</summary>
    [Test]
    public async Task MutualTlsOptional_UntrustedCertificateSucceedsAsFailed()
    {
        using X509Certificate2 untrusted = CreateUntrustedClientCertificate();
        TlsHarness.ServerCertificateHolder capture = new();
        using HttpClient client = TlsHarness.CreateClientPresentingCertificate(capture, untrusted);

        using HttpResponseMessage response = await PollAsync(
            client,
            static () => new HttpRequestMessage(HttpMethod.Get, "https://mtls-optional.local/"),
            static message => message.IsSuccessStatusCode,
            TlsPollSeconds);

        response.IsSuccessStatusCode.Should().BeTrue();
        JsonElement echo = await ReadJsonAsync(response);
        HeaderValue(echo.GetProperty("headers"), "X-SSL-Client-Verify").Should().Be("FAILED");
    }

    /// <summary>An <c>optional</c> mutual-TLS host reports SUCCESS with subject/issuer for a certificate that
    /// does chain to the configured CA — mirrors <see cref="MutualTls_AcceptsValidClientCertificate"/> on the
    /// optional (not required) host, proving SUCCESS isn't tied to the route being Required.</summary>
    [Test]
    public async Task MutualTlsOptional_ValidCertificateSucceedsAsSuccess()
    {
        TlsHarness.ServerCertificateHolder capture = new();
        using HttpClient client = TlsHarness.CreateMutualTlsClient(capture);

        using HttpResponseMessage response = await PollAsync(
            client,
            static () => new HttpRequestMessage(HttpMethod.Get, "https://mtls-optional.local/"),
            static message => message.IsSuccessStatusCode,
            TlsPollSeconds);

        response.IsSuccessStatusCode.Should().BeTrue();
        JsonElement echo = await ReadJsonAsync(response);
        JsonElement headers = echo.GetProperty("headers");
        HeaderValue(headers, "X-SSL-Client-Verify").Should().Be("SUCCESS");
        HeaderValue(headers, "X-SSL-Client-S-DN").Should().NotBeEmpty();
    }

    /// <summary>A required mutual-TLS host rejects a client certificate whose serial number is listed in the
    /// configured CRL, even though it chains to the same CA as the accepted certificate — proves the CRL check
    /// runs for real against a real handshake, not just the unit-level <c>Validate()</c> call.</summary>
    /// <remarks>
    /// Unlike a missing certificate (accepted at the handshake, rejected with 403 at the app layer — see
    /// <see cref="MutualTls_RejectsWithoutClientCertificate"/>), an invalid/revoked certificate on a
    /// <c>required</c> host fails the TLS handshake itself: the strict validation callback returns
    /// <see langword="false"/>, so the connection never reaches the app layer at all. The client observes a
    /// failed handshake, not an HTTP response.
    /// </remarks>
    [Test]
    public async Task MutualTlsRequired_RevokedCertificateIsRejected()
    {
        // Establish the route is live first, with a request that must succeed, before asserting the revoked
        // one fails — otherwise an early "not yet discovered" failure could be mistaken for revocation working.
        TlsHarness.ServerCertificateHolder readinessCapture = new();
        using HttpClient readinessClient = TlsHarness.CreateMutualTlsClient(readinessCapture);
        using HttpResponseMessage readinessResponse = await PollAsync(
            readinessClient,
            static () => new HttpRequestMessage(HttpMethod.Get, "https://mtls.local/"),
            static message => message.IsSuccessStatusCode,
            TlsPollSeconds);
        readinessResponse.IsSuccessStatusCode.Should().BeTrue();

        TlsHarness.ServerCertificateHolder capture = new();
        using HttpClient client = TlsHarness.CreateClientPresentingCertificate(capture, TlsHarness.RevokedClientCertificate);
        Func<Task> revokedRequest = async () =>
        {
            using HttpResponseMessage response = await client.GetAsync("https://mtls.local/");
        };

        await revokedRequest.Should().ThrowAsync<HttpRequestException>();
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

    // A self-signed leaf unrelated to TlsHarness's client CA — a client certificate an optional host must accept
    // the connection for, but never treat as trusted. Same PKCS#12 round-trip as TlsHarness's own certificates
    // (SChannel cannot use the ephemeral CNG key from CopyWithPrivateKey for client authentication on Windows).
    private static X509Certificate2 CreateUntrustedClientCertificate()
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new(
            "CN=intruder.local", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        OidCollection clientAuth = [];
        clientAuth.Add(new Oid("1.3.6.1.5.5.7.3.2"));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(clientAuth, false));

        // Unlike Create(issuer, ...) (used for CA-issued leaves elsewhere in this file), CreateSelfSigned already
        // returns a certificate with its private key attached — CopyWithPrivateKey would throw
        // InvalidOperationException ("already has an associated private key") if called here.
        using X509Certificate2 leaf =
            request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return X509CertificateLoader.LoadPkcs12(leaf.Export(X509ContentType.Pkcs12), password: null);
    }
}
