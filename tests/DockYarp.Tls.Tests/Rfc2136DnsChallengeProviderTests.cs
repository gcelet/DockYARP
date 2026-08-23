namespace DockYarp.Tls.Tests;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Tls;

/// <summary>Tests <see cref="Rfc2136DnsChallengeProvider"/> against a real local UDP listener standing in
/// for a DNS server — no live BIND9/network dependency, but the actual send/receive/RCODE-handling code
/// path runs for real (the e2e suite covers the real-server case against a genuine BIND9 container).</summary>
public sealed class Rfc2136DnsChallengeProviderTests
{
    [Test]
    public Task PublishTxtRecordAsync_ThrowsWhenConfigurationIsIncomplete()
    {
        Rfc2136DnsChallengeProvider provider = new(new TlsOptions());

        Func<Task> act = () => provider.PublishTxtRecordAsync("_acme-challenge.example.com", "abc", CancellationToken.None);

        return act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*DnsUpdateServer*");
    }

    [Test]
    public async Task PublishTxtRecordAsync_SucceedsWhenServerRespondsNoError()
    {
        await using UdpEchoServer server = UdpEchoServer.RespondingWithRcode(0);
        Rfc2136DnsChallengeProvider provider = new(OptionsFor(server.Port));

        Func<Task> act = () => provider.PublishTxtRecordAsync("_acme-challenge.example.com", "abc", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task PublishTxtRecordAsync_ThrowsWhenServerRejectsTheUpdate()
    {
        await using UdpEchoServer server = UdpEchoServer.RespondingWithRcode(5); // REFUSED
        Rfc2136DnsChallengeProvider provider = new(OptionsFor(server.Port));

        Func<Task> act = () => provider.PublishTxtRecordAsync("_acme-challenge.example.com", "abc", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*RCODE 5*");
    }

    private static TlsOptions OptionsFor(int port) => new()
    {
        DnsUpdateServer = $"127.0.0.1:{port}",
        DnsUpdateZone = "example.com.",
        DnsUpdateTsigKeyName = "test-key.",
        DnsUpdateTsigKeySecret = Convert.ToBase64String("secret"u8),
    };

    /// <summary>A minimal UDP server that reads one DNS message and replies with a fixed RCODE, mirroring
    /// just enough of a real DNS server's response shape for <see cref="Rfc2136DnsChallengeProvider"/> to
    /// interpret it.</summary>
    private sealed class UdpEchoServer : IAsyncDisposable
    {
        private readonly UdpClient socket;
        private readonly CancellationTokenSource cts = new();
        private readonly Task loop;

        private UdpEchoServer(byte rcode)
        {
            socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            Port = ((IPEndPoint)socket.Client.LocalEndPoint!).Port;
            loop = RunAsync(rcode, cts.Token);
        }

        public int Port { get; }

        public static UdpEchoServer RespondingWithRcode(byte rcode) => new(rcode);

        public async ValueTask DisposeAsync()
        {
            await cts.CancelAsync().ConfigureAwait(false);
            socket.Dispose();
            await loop.ConfigureAwait(false); // The listener loop's own try/catch swallows cancellation/disposal.
            cts.Dispose();
        }

        private async Task RunAsync(byte rcode, CancellationToken cancellationToken)
        {
            try
            {
                UdpReceiveResult request = await socket.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                byte[] response = [request.Buffer[0], request.Buffer[1], 0x00, rcode, 0, 0, 0, 0, 0, 0, 0, 0];
                await socket.SendAsync(response, request.RemoteEndPoint, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Test tore the server down before a request arrived — fine.
            }
            catch (ObjectDisposedException)
            {
                // Same as above, via the socket path instead of the token.
            }
        }
    }
}
