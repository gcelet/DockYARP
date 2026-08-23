namespace DockYarp.Tls;

using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Publishes/removes ACME DNS-01 TXT records via RFC 2136 (Dynamic DNS Update), authenticated with
/// a TSIG key (RFC 8945).</summary>
/// <remarks>Configuration completeness is validated lazily, on first use — not at construction — so an
/// unconfigured provider only fails the specific host that requests a <c>dns-01</c> challenge; hosts using
/// HTTP-01 never touch it. See <see cref="TlsOptions"/>'s <c>DnsUpdate*</c> properties.</remarks>
/// <param name="options">TLS options carrying the RFC 2136 server/zone/TSIG configuration.</param>
public sealed class Rfc2136DnsChallengeProvider(TlsOptions options) : IDnsChallengeProvider
{
    private const int DefaultPort = 53;
    private const int ReceiveTimeoutMs = 5000;

    /// <inheritdoc />
    public Task PublishTxtRecordAsync(string fqdn, string value, CancellationToken cancellationToken)
    {
        Rfc2136Configuration config = ResolveConfiguration();
        byte[] message = DnsUpdateMessage.BuildAddTxt(config.Zone, fqdn, value, TimeSpan.FromSeconds(60), config.Key);
        return SendAsync(config.Host, config.Port, message, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveTxtRecordAsync(string fqdn, string value, CancellationToken cancellationToken)
    {
        Rfc2136Configuration config = ResolveConfiguration();
        byte[] message = DnsUpdateMessage.BuildDeleteTxt(config.Zone, fqdn, value, config.Key);
        return SendAsync(config.Host, config.Port, message, cancellationToken);
    }

    private Rfc2136Configuration ResolveConfiguration()
    {
        if (options.DnsUpdateServer is not { Length: > 0 } server
            || options.DnsUpdateZone is not { Length: > 0 } zone
            || options.DnsUpdateTsigKeyName is not { Length: > 0 } keyName
            || options.DnsUpdateTsigKeySecret is not { Length: > 0 } keySecret)
        {
            throw new InvalidOperationException(
                "DNS-01 requires Tls:DnsUpdateServer, Tls:DnsUpdateZone, Tls:DnsUpdateTsigKeyName, and " +
                "Tls:DnsUpdateTsigKeySecret to be configured.");
        }

        (string host, int port) = ParseServer(server);
        TsigKey key = TsigKey.Parse(keyName, keySecret, options.DnsUpdateTsigAlgorithm);
        return new Rfc2136Configuration(host, port, zone, key);
    }

    private sealed record Rfc2136Configuration(string Host, int Port, string Zone, TsigKey Key);

    private static (string Host, int Port) ParseServer(string server)
    {
        int colon = server.LastIndexOf(':');
        return colon > 0 && int.TryParse(server[(colon + 1)..], out int port)
            ? (server[..colon], port)
            : (server, DefaultPort);
    }

    private static async Task SendAsync(string host, int port, byte[] message, CancellationToken cancellationToken)
    {
        byte[] response = await SendUdpAsync(host, port, message, cancellationToken).ConfigureAwait(false);
        if (IsTruncated(response))
        {
            response = await SendTcpAsync(host, port, message, cancellationToken).ConfigureAwait(false);
        }

        EnsureSuccess(response);
    }

    private static async Task<byte[]> SendUdpAsync(string host, int port, byte[] message, CancellationToken cancellationToken)
    {
        using UdpClient client = new();
        client.Client.ReceiveTimeout = ReceiveTimeoutMs;
        await client.SendAsync(message, host, port, cancellationToken).ConfigureAwait(false);

        using CancellationTokenSource timeout = new(ReceiveTimeoutMs);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        UdpReceiveResult result = await client.ReceiveAsync(linked.Token).ConfigureAwait(false);
        return result.Buffer;
    }

    private static async Task<byte[]> SendTcpAsync(string host, int port, byte[] message, CancellationToken cancellationToken)
    {
        using TcpClient client = new();
        await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        await using NetworkStream stream = client.GetStream();

        byte[] lengthPrefix = [(byte)(message.Length >> 8), (byte)message.Length];
        await stream.WriteAsync(lengthPrefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(message, cancellationToken).ConfigureAwait(false);

        byte[] responseLengthBuffer = new byte[2];
        await ReadExactAsync(stream, responseLengthBuffer, cancellationToken).ConfigureAwait(false);
        int responseLength = (responseLengthBuffer[0] << 8) | responseLengthBuffer[1];
        byte[] response = new byte[responseLength];
        await ReadExactAsync(stream, response, cancellationToken).ConfigureAwait(false);
        return response;
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new IOException("The DNS server closed the connection before sending a complete response.");
            }

            offset += read;
        }
    }

    // Header byte 2, bit 0x02 is the TC (truncated) flag.
    private static bool IsTruncated(byte[] response) => response.Length > 2 && (response[2] & 0x02) != 0;

    private static void EnsureSuccess(byte[] response)
    {
        if (response.Length < 4)
        {
            throw new InvalidOperationException("The DNS server returned a malformed response.");
        }

        int rcode = response[3] & 0x0F;
        if (rcode != 0)
        {
            throw new InvalidOperationException($"The DNS UPDATE was rejected by the server (RCODE {rcode}).");
        }
    }
}
