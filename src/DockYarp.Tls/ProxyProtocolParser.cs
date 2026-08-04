namespace DockYarp.Tls;

using System;
using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

/// <summary>Parses PROXY protocol (v1 text and v2 binary) headers into the real client endpoint.</summary>
/// <remarks>
/// Pure and incremental: <see cref="Parse"/> reports <see cref="ProxyProtocolParseStatus.NeedMoreData"/> until
/// the buffer holds a complete header, so a connection reader can drive it across partial reads. Only the
/// source (client) address is recovered; the destination is ignored.
/// </remarks>
public static class ProxyProtocolParser
{
    private const int V1MaxLength = 108; // "PROXY ..." line including CRLF (spec caps at 107); reject beyond this
    private const byte V2FirstByte = 0x0D;

    private static readonly byte[] V2Signature =
        [0x0D, 0x0A, 0x0D, 0x0A, 0x00, 0x0D, 0x0A, 0x51, 0x55, 0x49, 0x54, 0x0A];

    private static ProxyProtocolParseResult NeedMore =>
        new(ProxyProtocolParseStatus.NeedMoreData, default, 0);

    private static ProxyProtocolParseResult Invalid =>
        new(ProxyProtocolParseStatus.Invalid, default, 0);

    /// <summary>Attempts to parse a PROXY protocol header at the start of <paramref name="buffer"/>.</summary>
    /// <param name="buffer">The connection bytes seen so far.</param>
    /// <returns>The parse result: complete (with header + consumed byte count), needs more data, or invalid.</returns>
    public static ProxyProtocolParseResult Parse(ReadOnlySpan<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return NeedMore;
        }

        if (buffer[0] == V2FirstByte)
        {
            return ParseV2(buffer);
        }

        return buffer[0] == (byte)'P' ? ParseV1(buffer) : Invalid;
    }

    private static ProxyProtocolParseResult Done(ProxyProtocolHeader header, int consumed) =>
        new(ProxyProtocolParseStatus.Done, header, consumed);

    private static ProxyProtocolParseResult ParseV2(ReadOnlySpan<byte> buffer)
    {
        int prefix = Math.Min(buffer.Length, V2Signature.Length);
        if (!buffer[..prefix].SequenceEqual(V2Signature.AsSpan(0, prefix)))
        {
            return Invalid;
        }

        if (buffer.Length < 16)
        {
            return NeedMore;
        }

        if ((buffer[12] >> 4) != 0x2)
        {
            return Invalid;
        }

        int command = buffer[12] & 0x0F;
        int family = buffer[13] >> 4;
        int transport = buffer[13] & 0x0F;
        int addressLength = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(14, 2));
        int total = 16 + addressLength;
        if (buffer.Length < total)
        {
            return NeedMore;
        }

        // Command 0 is LOCAL (the balancer speaks for itself, e.g. health checks): no client address.
        if (command == 0)
        {
            return Done(new ProxyProtocolHeader(null), total);
        }

        // Only LOCAL (0) and PROXY (1) are defined.
        if (command != 1)
        {
            return Invalid;
        }

        IPEndPoint? source = ReadV2Source(buffer.Slice(16, addressLength), family, transport);
        return Done(new ProxyProtocolHeader(source), total);
    }

    private static IPEndPoint? ReadV2Source(ReadOnlySpan<byte> address, int family, int transport)
    {
        const int stream = 1;

        // AF_INET: 4-byte src + 4-byte dst + 2-byte src port + 2-byte dst port.
        if (family == 1 && transport == stream && address.Length >= 12)
        {
            return new IPEndPoint(new IPAddress(address[..4]), BinaryPrimitives.ReadUInt16BigEndian(address.Slice(8, 2)));
        }

        // AF_INET6: 16-byte src + 16-byte dst + 2-byte src port + 2-byte dst port.
        if (family == 2 && transport == stream && address.Length >= 36)
        {
            return new IPEndPoint(new IPAddress(address[..16]), BinaryPrimitives.ReadUInt16BigEndian(address.Slice(32, 2)));
        }

        // UNSPEC / UNIX / non-stream: a valid header with no usable client address.
        return null;
    }

    private static ProxyProtocolParseResult ParseV1(ReadOnlySpan<byte> buffer)
    {
        int prefix = Math.Min(buffer.Length, 6);
        if (!buffer[..prefix].SequenceEqual("PROXY "u8[..prefix]))
        {
            return Invalid;
        }

        if (buffer.Length < 6)
        {
            return NeedMore;
        }

        ReadOnlySpan<byte> window = buffer.Length <= V1MaxLength ? buffer : buffer[..V1MaxLength];
        int crlf = window.IndexOf("\r\n"u8);
        if (crlf < 0)
        {
            return buffer.Length < V1MaxLength ? NeedMore : Invalid;
        }

        return ParseV1Line(Encoding.ASCII.GetString(buffer[6..crlf]), crlf + 2);
    }

    private static ProxyProtocolParseResult ParseV1Line(string line, int consumed)
    {
        string[] parts = line.Split(' ');
        string protocol = parts[0];

        // UNKNOWN: the sender could not determine the addresses; the rest of the line is ignored.
        if (string.Equals(protocol, "UNKNOWN", StringComparison.Ordinal))
        {
            return Done(new ProxyProtocolHeader(null), consumed);
        }

        bool ipv4 = string.Equals(protocol, "TCP4", StringComparison.Ordinal);
        if ((!ipv4 && !string.Equals(protocol, "TCP6", StringComparison.Ordinal)) || parts.Length != 5)
        {
            return Invalid;
        }

        if (!IPAddress.TryParse(parts[1], out IPAddress? source)
            || !ushort.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out ushort port))
        {
            return Invalid;
        }

        AddressFamily expected = ipv4 ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6;
        return source.AddressFamily == expected
            ? Done(new ProxyProtocolHeader(new IPEndPoint(source, port)), consumed)
            : Invalid;
    }
}
