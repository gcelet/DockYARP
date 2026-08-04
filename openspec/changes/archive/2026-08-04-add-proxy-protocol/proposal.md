## Why
Behind an L4 load balancer (AWS NLB, HAProxy), the real client IP is only available via the PROXY protocol.
nginx-proxy supports `ENABLE_PROXY_PROTOCOL`; DockYarp does not, so `X-Forwarded-For`/`X-Real-IP` carry the
load balancer's address, not the client's. (The item also tracked custom external listener ports; those are
already delivered — `Server:HttpPort`/`HttpsPort` and per-vhost `EXTERNAL_HTTPS_PORT` — so this change is
PROXY-protocol only.)

## What Changes
- Add `Server:EnableProxyProtocol` (default `false`). When enabled, each edge connection begins with a PROXY
  protocol header (v1 text or v2 binary); DockYarp parses it and sets the connection's remote endpoint to the
  real client address before HTTP processing, so the existing forwarded-header logic carries the real client.
- A `LOCAL` (v2) / `UNKNOWN` (v1) header carries no address (e.g. the balancer's own health checks); the
  connection's transport peer is left unchanged. A malformed header on an enabled listener aborts the
  connection. When disabled, connections are handled exactly as before (no header expected).

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `deployment`: the edge listeners can accept the PROXY protocol and recover the real client address.

## Impact
- **Code**: `DockYarp.Tls` — `ProxyProtocolParser` (v1/v2 → source endpoint), `ProxyProtocolConnectionMiddleware`
  (reads the header off the connection, sets `RemoteEndPoint`), `ServerEndpointOptions.EnableProxyProtocol`,
  `KestrelTlsConfigurator` attaches the connection middleware to both listeners when enabled.
- **Tests (unit)**: `ProxyProtocolParser` — v1 TCP4/TCP6/UNKNOWN, v2 INET/INET6/LOCAL, truncated (need-more),
  malformed (invalid); `ProxyProtocolConnectionMiddleware` — over an in-memory connection pipe, a v1/v2 header
  sets the remote endpoint and the HTTP bytes after it remain readable.
- **Docs**: the site configuration reference documents `Server:EnableProxyProtocol`.
- **Runtime / e2e**: the parser and connection middleware are unit-tested on Windows; the live path behind a
  real L4 balancer is a Docker/e2e concern.
- **Owning agent**: AG-DEP. Resolves `add-proxy-protocol` (PROXY protocol; external ports already done).
