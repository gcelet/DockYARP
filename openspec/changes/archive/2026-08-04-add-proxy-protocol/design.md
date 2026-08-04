# Design — add-proxy-protocol

## Scope
PROXY protocol only. The item also mentioned custom external ports, but those already ship
(`Server:HttpPort`/`HttpsPort` bind the listeners; per-vhost `EXTERNAL_HTTPS_PORT` sets the redirect target —
`add-external-port-config`). No native Kestrel PROXY-protocol support exists in .NET, so this is a connection
middleware feeding the parser.

## Parser (pure, unit-tested — the clean core)
`ProxyProtocolParser.Parse(ReadOnlySpan<byte> buffer, out ProxyProtocolHeader header, out int consumed)`
returns a tri-state so the connection middleware can drive incremental reads:
```
enum ProxyProtocolParseStatus { NeedMoreData, Invalid, Done }
```
- **v1** (text): `PROXY TCP4|TCP6|UNKNOWN <src> <dst> <sport> <dport>\r\n` (≤ 108 bytes). `UNKNOWN` → parsed as
  header with no source endpoint. Missing CRLF within the cap → `NeedMoreData`; past the cap or malformed →
  `Invalid`.
- **v2** (binary): 12-byte signature + version/command + family/transport + 2-byte address length + addresses.
  `PROXY` command with `AF_INET`/`AF_INET6` `STREAM` → source `IPEndPoint`; `LOCAL`, `UNSPEC`, or `UNIX` →
  header with no source endpoint. Truncated → `NeedMoreData`; wrong version/signature → `Invalid`.
- `ProxyProtocolHeader` (readonly struct): `SourceEndPoint` (`IPEndPoint?`, null when the header carries no
  usable client address). Only the source is needed (the real client); the destination is ignored.

## Connection middleware (unit-tested over an in-memory pipe)
`ProxyProtocolConnectionMiddleware` wraps the next `ConnectionDelegate`:
```
read from connection.Transport.Input until Parse != NeedMoreData
  Invalid -> abort the connection
  Done    -> if header.SourceEndPoint is not null: connection.RemoteEndPoint = it
             advance the PipeReader past `consumed` bytes (the HTTP bytes after the header stay buffered)
await next(connection)
```
Because it reads from `connection.Transport` (an `IDuplexPipe`), it is tested with a fake `ConnectionContext`
whose transport is an in-memory `System.IO.Pipelines` pair preloaded with `<header><http bytes>` — asserting
`RemoteEndPoint` is set and the post-header bytes remain readable. No socket, deterministic on Windows.

## Wiring
`ServerEndpointOptions.EnableProxyProtocol` (bound from `Server`, default `false`). When set,
`KestrelTlsConfigurator` calls `listen.Use(next => middleware)` on both the HTTP and HTTPS listeners, before
`UseHttps`, so the real client address is set for TLS/HTTP alike.

## Out of scope
- External listener ports (already configurable).
- PROXY protocol on outbound/backend connections (this is inbound edge only).
