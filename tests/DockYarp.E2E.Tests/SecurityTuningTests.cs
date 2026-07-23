namespace DockYarp.E2E.Tests;

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using AwesomeAssertions;

/// <summary>Security and proxy-tuning scenarios driven from container labels.</summary>
[Category("EndToEnd")]
public sealed class SecurityTuningTests : E2ETestBase
{
    /// <summary>A Basic-Auth-protected route rejects a request without credentials.</summary>
    [Test]
    public async Task BasicAuth_RejectsWithoutCredentials()
    {
        using HttpResponseMessage response = await PollAsync(
            () => Request(HttpMethod.Get, "auth.local", "/"),
            static response => response.StatusCode == HttpStatusCode.Unauthorized);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>The same route accepts a request carrying valid Basic-Auth credentials.</summary>
    [Test]
    public async Task BasicAuth_AcceptsWithCredentials()
    {
        string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("alice:secret"));

        using HttpResponseMessage response = await PollAsync(
            () => Authorized(credentials),
            static response => response.IsSuccessStatusCode);

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    /// <summary><c>DOCKYARP_MAX_BODY_SIZE</c> makes the proxy reject an oversized request body.</summary>
    [Test]
    public async Task MaxBodySize_RejectsOversizedBody()
    {
        using HttpResponseMessage response = await PollAsync(
            Oversized,
            static response => response.StatusCode == HttpStatusCode.RequestEntityTooLarge);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    /// <summary><c>DOCKYARP_PROXY_TIMEOUT</c> cancels a backend response slower than the limit.</summary>
    [Test]
    public async Task ProxyTimeout_CancelsSlowResponse()
    {
        using HttpResponseMessage response = await PollAsync(
            () => Request(HttpMethod.Get, "limits.local", "/slow?ms=3000"),
            static response => response.StatusCode is HttpStatusCode.GatewayTimeout or HttpStatusCode.BadGateway);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.GatewayTimeout, HttpStatusCode.BadGateway);
    }

    private static HttpRequestMessage Authorized(string credentials)
    {
        HttpRequestMessage request = Request(HttpMethod.Get, "auth.local", "/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return request;
    }

    private static HttpRequestMessage Oversized()
    {
        HttpRequestMessage request = Request(HttpMethod.Post, "limits.local", "/");
        request.Content = new ByteArrayContent(new byte[2048]);
        return request;
    }
}
