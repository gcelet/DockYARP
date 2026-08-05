---
id: add-per-vhost-http2-toggle
capability: tls-acme
agent: AG-AT
tier: B-runtime
priority: low
status: backlog
nginx-proxy: com.github.nginx-proxy.nginx-proxy.http2.enable label (+ ENABLE_HTTP2)
provenance: 2026-08-05 parity re-comparison
---

## Why
nginx-proxy lets a vhost enable/disable HTTP/2 individually via the
`com.github.nginx-proxy.nginx-proxy.http2.enable` label (overriding the global `ENABLE_HTTP2`). DockYarp's
HTTP/2 is a **global** listener setting, so a single host cannot opt out of (or into) HTTP/2.

## nginx-proxy behavior
- Global `ENABLE_HTTP2` (default `true`) emits `http2 on;`; the per-vhost label overrides it for that server
  block only.

## DockYarp today
- HTTP protocols are set globally on the HTTPS listener (`Tls:HttpProtocols`, default `Http1AndHttp2`). The
  per-connection SNI handshake (`SniTlsHandshakeCallback`) assembles per-host TLS options, but ALPN /
  protocol negotiation is currently uniform across hosts.

## Proposed change (sketch)
- Recognize a per-host HTTP/2 enable/disable (label + env), and apply it in `SniTlsHandshakeCallback` by
  setting `SslServerAuthenticationOptions.ApplicationProtocols` per host — restricting ALPN to `http/1.1` for a
  host that disables HTTP/2 while others keep `h2`.

## Acceptance criteria (→ scenarios)
- **WHEN** a host disables HTTP/2 **THEN** its TLS connections negotiate HTTP/1.1 only.
- **WHEN** a host leaves it default **THEN** it keeps the global protocol set (HTTP/2 offered).

## Notes / risks / references
- **Niche / low value.** Runtime: ALPN negotiation needs an e2e to prove (batch with the TLS e2e).
- `SniTlsHandshakeCallback` already builds per-host `SslServerAuthenticationOptions`, so per-host
  `ApplicationProtocols` is a natural extension.
- Related: `finish-http3` (the per-vhost `http3.enable` label folds in there).
