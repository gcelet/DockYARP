namespace DockYarp.E2E.Tests;

using System;
using System.Net.Http;
using System.Security.Authentication;
using System.Text.Json;
using System.Threading.Tasks;

using AwesomeAssertions;

/// <summary>Per-host SSL_POLICY negotiated live: a Mozilla-Modern host refuses TLS 1.2; a global-posture host accepts it.</summary>
[Category("EndToEnd")]
public sealed class SslPolicyNegotiationTests : E2ETestBase
{
    private const int TlsPollSeconds = 60;

    /// <summary><c>modern.local</c> (SSL_POLICY=Mozilla-Modern) negotiates only TLS 1.3 — a TLS 1.2 handshake is refused.</summary>
    [Test]
    public async Task ModernHost_RefusesTls12_AcceptsTls13()
    {
        TlsHarness.ServerCertificateHolder capture = new();

        // Wait until modern.local is routed to its own backend over TLS 1.3. A TLS 1.3 handshake succeeds under
        // both the global and the Modern posture, so it is the backend id that proves the modern.local route —
        // and therefore its per-host policy — is live.
        using HttpClient tls13 = TlsHarness.CreateClientWithProtocol(capture, SslProtocols.Tls13);
        string? servedBy = await PollEchoIdAsync(tls13, "https://modern.local/", "sslpolicy");
        servedBy.Should().Be("sslpolicy");

        // The same host refuses a TLS 1.2-only handshake (Mozilla-Modern floors at TLS 1.3).
        using HttpClient tls12 =
            TlsHarness.CreateClientWithProtocol(new TlsHarness.ServerCertificateHolder(), SslProtocols.Tls12);
        Func<Task> tls12ToModern = async () =>
        {
            using HttpResponseMessage response = await tls12.GetAsync("https://modern.local/");
        };

        await tls12ToModern.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary><c>tls.local</c> declares no SSL_POLICY, so the global posture (min TLS 1.2) accepts a TLS 1.2 handshake.</summary>
    [Test]
    public async Task GlobalPostureHost_AcceptsTls12()
    {
        using HttpClient tls12 =
            TlsHarness.CreateClientWithProtocol(new TlsHarness.ServerCertificateHolder(), SslProtocols.Tls12);

        using HttpResponseMessage response = await PollAsync(
            tls12,
            static () => new HttpRequestMessage(HttpMethod.Get, "https://tls.local/"),
            static message => message.IsSuccessStatusCode,
            TlsPollSeconds);

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    // Polls the echo backend behind a vhost (via a TLS-pinned client) until it responds with the expected id.
    private static async Task<string?> PollEchoIdAsync(HttpClient client, string url, string expectedId)
    {
        long deadline = Environment.TickCount64 + (TlsPollSeconds * 1000L);
        string? id = null;
        while (true)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            using HttpResponseMessage response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                JsonElement echo = await ReadJsonAsync(response);
                id = EchoId(echo);
                if (string.Equals(id, expectedId, StringComparison.Ordinal))
                {
                    return id;
                }
            }

            if (Environment.TickCount64 > deadline)
            {
                return id;
            }

            await Task.Delay(500);
        }
    }
}
