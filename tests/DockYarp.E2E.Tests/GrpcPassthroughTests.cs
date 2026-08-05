namespace DockYarp.E2E.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.E2E.Grpc;

using global::Grpc.Core;
using global::Grpc.Net.Client;

/// <summary>A gRPC backend labeled <c>VIRTUAL_PROTO=grpc</c> is proxied end to end over HTTP/2 (unary + streaming).</summary>
[Category("EndToEnd")]
public sealed class GrpcPassthroughTests : E2ETestBase
{
    private const int GrpcPollSeconds = 60;

    /// <summary>A unary and a server-streaming gRPC call both proxy through DockYarp to the gRPC backend.</summary>
    [Test]
    public async Task UnaryAndServerStreamingProxyThroughDockYarp()
    {
        TlsHarness.ServerCertificateHolder capture = new();
        using GrpcChannel channel = TlsHarness.CreateGrpcChannel("grpc.local", capture);
        Echoer.EchoerClient client = new(channel);

        // Poll the unary call until the gRPC route is discovered and live (backend up + HTTP/2 cluster routed).
        EchoReply reply = await PollUnaryAsync(client);
        reply.Message.Should().Be("ping");

        // Server streaming proves HTTP/2 stream framing + trailers survive the proxy hop.
        using AsyncServerStreamingCall<EchoReply> call = client.EchoStream(new EchoRequest { Message = "x", Count = 3 });
        List<string> received = [];
        await foreach (EchoReply message in call.ResponseStream.ReadAllAsync())
        {
            received.Add(message.Message);
        }

        received.Should().Equal("x-0", "x-1", "x-2");
    }

    private static async Task<EchoReply> PollUnaryAsync(Echoer.EchoerClient client)
    {
        long deadline = Environment.TickCount64 + (GrpcPollSeconds * 1000L);
        while (true)
        {
            try
            {
                using AsyncUnaryCall<EchoReply> call = client.EchoAsync(new EchoRequest { Message = "ping" });
                return await call.ResponseAsync;
            }
            catch (RpcException) when (Environment.TickCount64 < deadline)
            {
                await Task.Delay(500);
            }
        }
    }
}
