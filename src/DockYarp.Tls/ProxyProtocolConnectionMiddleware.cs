namespace DockYarp.Tls;

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Connections;

/// <summary>Connection middleware that consumes a leading PROXY protocol header and recovers the client endpoint.</summary>
/// <remarks>
/// Runs before HTTP/TLS on an edge listener when <c>Server:EnableProxyProtocol</c> is set: it reads the header
/// off the transport, sets the connection's <c>RemoteEndPoint</c> to the real client, and leaves the following
/// bytes buffered for the next middleware. A malformed or oversized header aborts the connection.
/// </remarks>
/// <param name="next">The next connection delegate (HTTP, or TLS then HTTP).</param>
public sealed class ProxyProtocolConnectionMiddleware(ConnectionDelegate next)
{
    // The largest header we tolerate before declaring the connection malformed (v1 ≤ 108; v2 addresses are
    // small). Bounds buffering so a peer cannot pin memory by dribbling a never-completing header.
    private const int MaxHeaderBytes = 536;

    /// <summary>Consumes the PROXY header, sets the remote endpoint, then invokes the next delegate.</summary>
    /// <param name="connection">The incoming connection.</param>
    /// <returns>A task that completes when the connection has been handled.</returns>
    public async Task OnConnectionAsync(ConnectionContext connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (await TryConsumeHeaderAsync(connection).ConfigureAwait(false))
        {
            await next(connection).ConfigureAwait(false);
        }
    }

    private static async Task<bool> TryConsumeHeaderAsync(ConnectionContext connection)
    {
        PipeReader input = connection.Transport.Input;
        while (true)
        {
            ReadResult read = await input.ReadAsync().ConfigureAwait(false);
            ReadOnlySequence<byte> buffer = read.Buffer;
            ProxyProtocolParseResult result = Inspect(buffer);

            if (result.Status == ProxyProtocolParseStatus.Done)
            {
                if (result.Header.SourceEndPoint is { } source)
                {
                    connection.RemoteEndPoint = source;
                }

                input.AdvanceTo(buffer.GetPosition(result.Consumed));
                return true;
            }

            if (result.Status == ProxyProtocolParseStatus.Invalid || read.IsCompleted || buffer.Length >= MaxHeaderBytes)
            {
                connection.Abort();
                return false;
            }

            // Need more data: mark all of it examined so ReadAsync waits for further bytes.
            input.AdvanceTo(buffer.Start, buffer.End);
        }
    }

    private static ProxyProtocolParseResult Inspect(ReadOnlySequence<byte> buffer)
    {
        if (buffer.IsSingleSegment)
        {
            return ProxyProtocolParser.Parse(buffer.FirstSpan);
        }

        Span<byte> scratch = stackalloc byte[MaxHeaderBytes];
        int length = (int)Math.Min(buffer.Length, scratch.Length);
        buffer.Slice(0, length).CopyTo(scratch);
        return ProxyProtocolParser.Parse(scratch[..length]);
    }
}
