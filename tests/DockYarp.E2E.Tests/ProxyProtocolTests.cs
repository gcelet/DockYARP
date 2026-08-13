namespace DockYarp.E2E.Tests;

using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using AwesomeAssertions;

/// <summary>End-to-end: a PROXY protocol header on the edge recovers the real client IP into the forwarded headers.</summary>
/// <remarks>
/// Uses the dedicated DockYarp instance with <c>Server:EnableProxyProtocol=true</c> (the shared proxy cannot enable it
/// — every connection would then need a PROXY preamble). The test drives the plaintext HTTP edge with a raw socket so
/// the PROXY header precedes the request with no TLS in between, and asserts the echo backend sees the spoofed client
/// IP in <c>X-Real-IP</c> / <c>X-Forwarded-For</c> (proving the live Kestrel edge wiring, not just the unit-tested
/// parser/middleware).
/// </remarks>
[Category("EndToEnd")]
public sealed class ProxyProtocolTests
{
    private const string SpoofedClientIp = "203.0.113.7";
    private const int SpoofedClientPort = 12345;
    private const string BalancerIp = "10.0.0.1";
    private const int EdgePort = 8080;
    private const string EchoHost = "default.local"; // echo-default (BackendCatalog.DefaultHost) reflects headers as JSON
    private const int PollSeconds = 60;

    /// <summary>A PROXY v1 (text) header yields the real client IP in the forwarded headers.</summary>
    [Test]
    public async Task ProxyProtocolV1_YieldsRealClientIpInForwardedHeaders()
    {
        byte[] header = Encoding.ASCII.GetBytes(
            $"PROXY TCP4 {SpoofedClientIp} {BalancerIp} {SpoofedClientPort} {EdgePort}\r\n");

        JsonElement echo = await PollEchoWithProxyHeaderAsync(header);

        AssertSpoofedIp(echo);
    }

    /// <summary>A PROXY v2 (binary) header yields the real client IP in the forwarded headers.</summary>
    [Test]
    public async Task ProxyProtocolV2_YieldsRealClientIpInForwardedHeaders()
    {
        byte[] header = BuildProxyV2Ipv4(SpoofedClientIp, SpoofedClientPort, BalancerIp, EdgePort);

        JsonElement echo = await PollEchoWithProxyHeaderAsync(header);

        AssertSpoofedIp(echo);
    }

    private static void AssertSpoofedIp(JsonElement echo)
    {
        JsonElement headers = echo.GetProperty("headers");
        HeaderValue(headers, "X-Real-IP").Should().Be(SpoofedClientIp);
        HeaderValue(headers, "X-Forwarded-For").Should().Contain(SpoofedClientIp);
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

    // AF_INET PROXY v2 header: 12-byte signature, 0x21 (v2/PROXY), 0x11 (AF_INET/STREAM), a 12-byte address block
    // (src+dst IPv4 and ports, big-endian).
    private static byte[] BuildProxyV2Ipv4(string sourceIp, int sourcePort, string destinationIp, int destinationPort)
    {
        byte[] header = new byte[28];
        ReadOnlySpan<byte> signature = [0x0D, 0x0A, 0x0D, 0x0A, 0x00, 0x0D, 0x0A, 0x51, 0x55, 0x49, 0x54, 0x0A];
        signature.CopyTo(header);
        header[12] = 0x21;
        header[13] = 0x11;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(14, 2), 12);
        IPAddress.Parse(sourceIp).GetAddressBytes().CopyTo(header, 16);
        IPAddress.Parse(destinationIp).GetAddressBytes().CopyTo(header, 20);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(24, 2), (ushort)sourcePort);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(26, 2), (ushort)destinationPort);
        return header;
    }

    // Poll the dedicated edge (fresh socket per attempt) until the route is discovered and returns a live echo. A
    // connect/read failure or a non-200 means "not ready yet" and is retried within the budget.
    private static async Task<JsonElement> PollEchoWithProxyHeaderAsync(byte[] proxyHeader)
    {
        long deadline = Environment.TickCount64 + (PollSeconds * 1000L);
        while (true)
        {
            JsonElement? echo = await TrySendAsync(proxyHeader);
            if (echo is { } value && value.TryGetProperty("headers", out _))
            {
                return value;
            }

            if (Environment.TickCount64 > deadline)
            {
                throw new TimeoutException(
                    $"The proxy-protocol edge did not return a live echo for '{EchoHost}' within {PollSeconds}s.");
            }

            await Task.Delay(500);
        }
    }

    private static async Task<JsonElement?> TrySendAsync(byte[] proxyHeader)
    {
        Uri edge = AspireAppHostFixture.ProxyProtocolBaseAddress;
        try
        {
            using TcpClient client = new();
            await client.ConnectAsync(edge.Host, edge.Port);
            await using NetworkStream stream = client.GetStream();

            await stream.WriteAsync(proxyHeader);
            await stream.WriteAsync(Encoding.ASCII.GetBytes(
                $"GET / HTTP/1.1\r\nHost: {EchoHost}\r\nConnection: close\r\n\r\n"));
            await stream.FlushAsync();

            using MemoryStream sink = new();
            await stream.CopyToAsync(sink);
            string response = Encoding.UTF8.GetString(sink.ToArray());
            return ExtractJsonObject(response);
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            // The edge is not accepting yet (or aborted a not-yet-live connection) — treat as "retry".
            return null;
        }
    }

    // The echo body is a single JSON object; take the substring between the first '{' and last '}' so the heuristic
    // survives either Content-Length or a single chunked frame without a full HTTP-response parser.
    private static JsonElement? ExtractJsonObject(string response)
    {
        int start = response.IndexOf('{', StringComparison.Ordinal);
        int end = response.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(response[start..(end + 1)]);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
