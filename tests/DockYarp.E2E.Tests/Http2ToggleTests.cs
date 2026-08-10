namespace DockYarp.E2E.Tests;

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using AwesomeAssertions;

/// <summary>Per-host HTTP/2 toggle negotiated live: a default host serves HTTP/2, a DOCKYARP_HTTP2=false host serves HTTP/1.1.</summary>
[Category("EndToEnd")]
public sealed class Http2ToggleTests : E2ETestBase
{
    private const int TlsPollSeconds = 60;

    /// <summary><c>tls.local</c> declares no override, so a client offering HTTP/2 negotiates HTTP/2 via ALPN.</summary>
    [Test]
    public async Task DefaultHost_NegotiatesHttp2()
    {
        TlsHarness.ServerCertificateHolder capture = new();
        using HttpClient client = TlsHarness.CreateClient(capture);

        using HttpResponseMessage response = await PollAsync(
            client,
            static () => Http2Request("https://tls.local/"),
            static message => message.IsSuccessStatusCode,
            TlsPollSeconds);

        response.Version.Should().Be(HttpVersion.Version20);
    }

    /// <summary><c>http1.local</c> (<c>DOCKYARP_HTTP2=false</c>) advertises only HTTP/1.1, so the same client negotiates HTTP/1.1.</summary>
    [Test]
    public async Task DisabledHost_NegotiatesHttp11()
    {
        TlsHarness.ServerCertificateHolder capture = new();
        using HttpClient client = TlsHarness.CreateClient(capture);

        using HttpResponseMessage response = await PollAsync(
            client,
            static () => Http2Request("https://http1.local/"),
            static message => message.IsSuccessStatusCode,
            TlsPollSeconds);

        response.Version.Should().Be(HttpVersion.Version11);
    }

    // A request that prefers HTTP/2 but accepts HTTP/1.1, so the negotiated version reflects the server's ALPN.
    private static HttpRequestMessage Http2Request(string url) =>
        new(HttpMethod.Get, url)
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        };
}
