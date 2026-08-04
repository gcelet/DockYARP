namespace DockYarp.Tls;

/// <summary>The result of a PROXY protocol parse attempt.</summary>
/// <param name="Status">Whether the header is complete, needs more bytes, or is invalid.</param>
/// <param name="Header">The parsed header when <paramref name="Status"/> is <see cref="ProxyProtocolParseStatus.Done"/>.</param>
/// <param name="Consumed">The number of header bytes consumed (the payload follows them).</param>
public readonly record struct ProxyProtocolParseResult(
    ProxyProtocolParseStatus Status, ProxyProtocolHeader Header, int Consumed);
