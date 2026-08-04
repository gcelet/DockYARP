namespace DockYarp.Tls.Tests;

using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Tls;

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;

/// <summary>Tests for <see cref="ProxyProtocolConnectionMiddleware"/> over an in-memory connection.</summary>
public sealed class ProxyProtocolConnectionMiddlewareTests
{
    private const string HttpPayload = "GET / HTTP/1.1\r\nHost: app.local\r\n\r\n";

    private static readonly byte[] V2Signature =
        [0x0D, 0x0A, 0x0D, 0x0A, 0x00, 0x0D, 0x0A, 0x51, 0x55, 0x49, 0x54, 0x0A];

    /// <summary>A v1 header sets the remote endpoint and leaves the HTTP bytes for the next delegate.</summary>
    [Test]
    public async Task V1HeaderSetsRemoteEndpointAndPreservesPayload()
    {
        byte[] input = [.. Encoding.ASCII.GetBytes("PROXY TCP4 203.0.113.7 10.0.0.1 4321 443\r\n"), .. Encoding.ASCII.GetBytes(HttpPayload)];

        (EndPoint? remote, string payload, bool nextCalled, bool aborted) = await RunAsync(input);

        nextCalled.Should().BeTrue();
        aborted.Should().BeFalse();
        remote.Should().Be(new IPEndPoint(IPAddress.Parse("203.0.113.7"), 4321));
        payload.Should().Be(HttpPayload);
    }

    /// <summary>A v2 header sets the remote endpoint and leaves the HTTP bytes for the next delegate.</summary>
    [Test]
    public async Task V2HeaderSetsRemoteEndpointAndPreservesPayload()
    {
        byte[] header =
        [
            .. V2Signature, 0x21, 0x11, 0x00, 0x0C,
            198, 51, 100, 23,   // source 198.51.100.23
            10, 0, 0, 1,        // destination
            0x1F, 0x90,         // source port 8080
            0x01, 0xBB,
        ];
        byte[] input = [.. header, .. Encoding.ASCII.GetBytes(HttpPayload)];

        (EndPoint? remote, string payload, bool nextCalled, bool aborted) = await RunAsync(input);

        nextCalled.Should().BeTrue();
        aborted.Should().BeFalse();
        remote.Should().Be(new IPEndPoint(IPAddress.Parse("198.51.100.23"), 8080));
        payload.Should().Be(HttpPayload);
    }

    /// <summary>An invalid header aborts the connection and never invokes the next delegate.</summary>
    [Test]
    public async Task InvalidHeaderAbortsConnection()
    {
        byte[] input = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\n"); // no PROXY header on an enabled listener

        (EndPoint? _, string _, bool nextCalled, bool aborted) = await RunAsync(input);

        nextCalled.Should().BeFalse();
        aborted.Should().BeTrue();
    }

    private static async Task<(EndPoint? Remote, string Payload, bool NextCalled, bool Aborted)> RunAsync(byte[] input)
    {
        Pipe inbound = new();
        Pipe outbound = new();
        await inbound.Writer.WriteAsync(input);
        await inbound.Writer.CompleteAsync();

        TestConnection connection = new(new DuplexPipe(inbound.Reader, outbound.Writer));

        EndPoint? remote = null;
        string payload = string.Empty;
        bool nextCalled = false;
        ProxyProtocolConnectionMiddleware middleware = new(async context =>
        {
            nextCalled = true;
            remote = context.RemoteEndPoint;
            ReadResult read = await context.Transport.Input.ReadAsync();
            payload = Encoding.ASCII.GetString(read.Buffer.ToArray());
            context.Transport.Input.AdvanceTo(read.Buffer.End);
        });

        await middleware.OnConnectionAsync(connection);
        return (remote, payload, nextCalled, connection.Aborted);
    }

    private sealed class DuplexPipe(PipeReader input, PipeWriter output) : IDuplexPipe
    {
        public PipeReader Input { get; } = input;

        public PipeWriter Output { get; } = output;
    }

    private sealed class TestConnection(IDuplexPipe transport) : ConnectionContext
    {
        public bool Aborted { get; private set; }

        public override IDuplexPipe Transport { get; set; } = transport;

        public override string ConnectionId { get; set; } = "test";

        public override IFeatureCollection Features { get; } = new FeatureCollection();

        public override IDictionary<object, object?> Items { get; set; } = new Dictionary<object, object?>();

        public override void Abort() => Aborted = true;

        public override void Abort(ConnectionAbortedException abortReason) => Aborted = true;
    }
}
