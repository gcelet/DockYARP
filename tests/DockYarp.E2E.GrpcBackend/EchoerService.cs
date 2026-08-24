namespace DockYarp.E2E.GrpcBackend;

using System.Globalization;
using System.Threading.Tasks;

using DockYarp.E2E.Grpc;

using global::Grpc.Core;

/// <summary>Echo gRPC service: a unary echo and a server-streaming echo, for the proxy passthrough e2e.</summary>
public sealed class EchoerService : Echoer.EchoerBase
{
    /// <summary>Echoes the request message back to the caller.</summary>
    /// <param name="request">The echo request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A reply carrying the same message.</returns>
    public override Task<EchoReply> Echo(EchoRequest request, ServerCallContext context) =>
        Task.FromResult(new EchoReply { Message = request.Message });

    /// <summary>Streams <see cref="EchoRequest.Count"/> replies, each echoing the message with its index.</summary>
    /// <param name="request">The echo request (message + count).</param>
    /// <param name="responseStream">The server response stream.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A task that completes when every reply has been written.</returns>
    public override async Task EchoStream(
        EchoRequest request, IServerStreamWriter<EchoReply> responseStream, ServerCallContext context)
    {
        for (int index = 0; index < request.Count; index++)
        {
            string message = string.Create(CultureInfo.InvariantCulture, $"{request.Message}-{index}");
            await responseStream.WriteAsync(new EchoReply { Message = message }, context.CancellationToken).ConfigureAwait(false);
        }
    }
}
