namespace DockYarp.Tls.Tests;

using System.Net;
using System.Text;

using AwesomeAssertions;

using DockYarp.Tls;

/// <summary>Tests for <see cref="ProxyProtocolParser"/> (v1 text + v2 binary).</summary>
public sealed class ProxyProtocolParserTests
{
    private static readonly byte[] V2Signature =
        [0x0D, 0x0A, 0x0D, 0x0A, 0x00, 0x0D, 0x0A, 0x51, 0x55, 0x49, 0x54, 0x0A];

    /// <summary>A v1 TCP4 header yields the source IPv4 endpoint and consumes the whole line.</summary>
    [Test]
    public void V1Tcp4YieldsSourceEndpoint()
    {
        byte[] header = Ascii("PROXY TCP4 192.168.0.1 10.0.0.1 56324 443\r\n");

        ProxyProtocolParseResult result = ProxyProtocolParser.Parse(header);

        result.Status.Should().Be(ProxyProtocolParseStatus.Done);
        result.Header.SourceEndPoint.Should().Be(new IPEndPoint(IPAddress.Parse("192.168.0.1"), 56324));
        result.Consumed.Should().Be(header.Length);
    }

    /// <summary>A v1 TCP6 header yields the source IPv6 endpoint.</summary>
    [Test]
    public void V1Tcp6YieldsSourceEndpoint()
    {
        ProxyProtocolParseResult result =
            ProxyProtocolParser.Parse(Ascii("PROXY TCP6 2001:db8::1 2001:db8::2 1111 443\r\n"));

        result.Status.Should().Be(ProxyProtocolParseStatus.Done);
        result.Header.SourceEndPoint.Should().Be(new IPEndPoint(IPAddress.Parse("2001:db8::1"), 1111));
    }

    /// <summary>A v1 UNKNOWN header is valid but carries no client address.</summary>
    [Test]
    public void V1UnknownHasNoSource()
    {
        ProxyProtocolParseResult result = ProxyProtocolParser.Parse(Ascii("PROXY UNKNOWN\r\n"));

        result.Status.Should().Be(ProxyProtocolParseStatus.Done);
        result.Header.SourceEndPoint.Should().BeNull();
    }

    /// <summary>A v1 line without its terminating CRLF (within the cap) needs more data.</summary>
    [Test]
    public void V1WithoutCrlfNeedsMoreData()
    {
        ProxyProtocolParser.Parse(Ascii("PROXY TCP4 1.2.3.4")).Status
            .Should().Be(ProxyProtocolParseStatus.NeedMoreData);
    }

    /// <summary>A v1 header with a malformed address, family mismatch, or non-PROXY prefix is invalid.</summary>
    [Test]
    public void V1MalformedIsInvalid()
    {
        ProxyProtocolParser.Parse(Ascii("PROXY TCP4 not-an-ip 10.0.0.1 1 2\r\n")).Status
            .Should().Be(ProxyProtocolParseStatus.Invalid);
        ProxyProtocolParser.Parse(Ascii("PROXY TCP4 ::1 ::1 1 2\r\n")).Status
            .Should().Be(ProxyProtocolParseStatus.Invalid);
        ProxyProtocolParser.Parse(Ascii("PING\r\n")).Status
            .Should().Be(ProxyProtocolParseStatus.Invalid);
    }

    /// <summary>A v2 INET PROXY header yields the source IPv4 endpoint and consumes the whole header.</summary>
    [Test]
    public void V2InetYieldsSourceEndpoint()
    {
        byte[] header =
        [
            .. V2Signature,
            0x21, 0x11, 0x00, 0x0C, // v2 PROXY, AF_INET STREAM, address length 12
            1, 2, 3, 4,             // source 1.2.3.4
            5, 6, 7, 8,             // destination 5.6.7.8
            0x12, 0x34,             // source port 4660
            0x01, 0xBB,             // destination port 443
        ];

        ProxyProtocolParseResult result = ProxyProtocolParser.Parse(header);

        result.Status.Should().Be(ProxyProtocolParseStatus.Done);
        result.Header.SourceEndPoint.Should().Be(new IPEndPoint(IPAddress.Parse("1.2.3.4"), 4660));
        result.Consumed.Should().Be(header.Length);
    }

    /// <summary>A v2 INET6 PROXY header yields the source IPv6 endpoint.</summary>
    [Test]
    public void V2Inet6YieldsSourceEndpoint()
    {
        byte[] source = [0x20, 0x01, 0x0d, 0xb8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1]; // 2001:db8::1
        byte[] destination = [0x20, 0x01, 0x0d, 0xb8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2];
        byte[] header =
        [
            .. V2Signature,
            0x21, 0x21, 0x00, 0x24, // v2 PROXY, AF_INET6 STREAM, address length 36
            .. source,
            .. destination,
            0x04, 0x57,             // source port 1111
            0x01, 0xBB,
        ];

        ProxyProtocolParseResult result = ProxyProtocolParser.Parse(header);

        result.Status.Should().Be(ProxyProtocolParseStatus.Done);
        result.Header.SourceEndPoint.Should().Be(new IPEndPoint(new IPAddress(source), 1111));
    }

    /// <summary>A v2 LOCAL header is valid but carries no client address.</summary>
    [Test]
    public void V2LocalHasNoSource()
    {
        byte[] header = [.. V2Signature, 0x20, 0x00, 0x00, 0x00]; // v2 LOCAL, no address block

        ProxyProtocolParseResult result = ProxyProtocolParser.Parse(header);

        result.Status.Should().Be(ProxyProtocolParseStatus.Done);
        result.Header.SourceEndPoint.Should().BeNull();
    }

    /// <summary>A truncated v2 header (signature only) needs more data; a corrupt signature is invalid.</summary>
    [Test]
    public void V2TruncatedNeedsMoreAndBadSignatureIsInvalid()
    {
        ProxyProtocolParser.Parse(V2Signature).Status.Should().Be(ProxyProtocolParseStatus.NeedMoreData);

        byte[] corrupt = [0x0D, 0x0A, 0x0D, 0xFF, 0x00, 0x0D, 0x0A, 0x51, 0x55, 0x49, 0x54, 0x0A];
        ProxyProtocolParser.Parse(corrupt).Status.Should().Be(ProxyProtocolParseStatus.Invalid);
    }

    /// <summary>An empty buffer needs more data.</summary>
    [Test]
    public void EmptyBufferNeedsMoreData() =>
        ProxyProtocolParser.Parse([]).Status.Should().Be(ProxyProtocolParseStatus.NeedMoreData);

    private static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);
}
