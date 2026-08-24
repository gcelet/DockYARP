namespace DockYarp.E2E.Tests;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

/// <summary>Asserts that persisted state survives a container recreation: a provisioned certificate is reused.</summary>
[Category("EndToEnd")]
public sealed class RestartPersistenceTests : E2ETestBase
{
    // Cold ACME provisioning can be slow (HTTP-01 retries), and this test runs before the TLS suite warms it up
    // (NUnit orders classes alphabetically), so the pre-restart wait for a real certificate is generous.
    private const int ProvisionPollSeconds = 150;

    // After the restart the certificate is reloaded from disk, so it should be served promptly.
    private const int ReusePollSeconds = 90;

    // The step-ca CA name (DOCKER_STEPCA_INIT_NAME) appears in the issuer of ACME-issued certificates.
    private const string CaIssuerMarker = "DockYarp E2E";

    // Recreating the container and waiting for health can take a while under DCP.
    private const int RestartTimeoutSeconds = 180;

    /// <summary>The certificate provisioned for a TLS host is reused (same thumbprint) after the container restarts.</summary>
    /// <remarks>
    /// The e2e sets a short renewal margin so the provisioned certificate is not renewed mid-run; a stable
    /// thumbprint before and after a real container restart proves the certificate was reloaded from the persisted
    /// <c>/certs</c> volume rather than re-provisioned — the same volume that also carries the Data Protection keys.
    /// </remarks>
    [Test]

    // Tls__CheckInterval is 5s in this AppHost, so a retry lets a reconciliation pass that outran the pre-restart
    // poll window still be observed instead of failing the whole run on a transient step-ca hiccup.
    [Retry(2)]
    public async Task ProvisionedCertificate_IsReusedAfterRestart()
    {
        // Wait for a genuinely ACME-issued certificate before restarting: if tls.local is still on the self-signed
        // fallback there is nothing persisted to reuse, so the assertion below would be meaningless.
        string thumbprintBefore = await ServedAcmeThumbprintAsync(ProvisionPollSeconds);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(RestartTimeoutSeconds));
        await AspireAppHostFixture.RestartProxyAndWaitHealthyAsync(cts.Token);

        string thumbprintAfter = await ServedAcmeThumbprintAsync(ReusePollSeconds);

        thumbprintAfter.Should().Be(
            thumbprintBefore,
            "the certificate persisted to the mounted volume should be reused across a container recreation, not re-provisioned");
    }

    // Polls tls.local over HTTPS until the served certificate is the ACME-issued one and returns its thumbprint.
    // Asserts the certificate really is ACME-issued so a timed-out poll (still on the self-signed fallback) fails
    // loudly instead of returning the fallback thumbprint.
    private static async Task<string> ServedAcmeThumbprintAsync(int timeoutSeconds)
    {
        TlsHarness.ServerCertificateHolder capture = new();
        using HttpClient client = TlsHarness.CreateClient(capture);

        using HttpResponseMessage response = await PollAsync(
            client,
            static () => new HttpRequestMessage(HttpMethod.Get, "https://tls.local/"),
            _ => capture.ServerCertificate?.Issuer.Contains(CaIssuerMarker, StringComparison.Ordinal) == true,
            timeoutSeconds);

        capture.ServerCertificate.Should().NotBeNull();
        capture.ServerCertificate!.Issuer.Should().Contain(
            CaIssuerMarker,
            "the restart-persistence assertion needs a real ACME certificate, not the self-signed fallback");
        return capture.ServerCertificate.Thumbprint;
    }
}
