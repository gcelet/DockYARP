---
id: add-proxy-protocol
capability: deployment
agent: AG-DEP
tier: B-runtime
priority: low
status: backlog
nginx-proxy: ENABLE_PROXY_PROTOCOL (+ HTTP_PORT / EXTERNAL_*_PORT)
provenance: this parity pass (matrix: PROXY protocol ⛔; custom external ports folded here)
---

## Why
Behind an L4 load balancer (AWS NLB, HAProxy), the real client IP is only available via the PROXY protocol.
nginx-proxy supports `ENABLE_PROXY_PROTOCOL`. DockYarp cannot accept PROXY protocol, so `X-Forwarded-For`/
`X-Real-IP` reflect the LB, not the client. This item also tracks custom **external listener ports**.

## nginx-proxy behavior
- `ENABLE_PROXY_PROTOCOL` makes nginx accept the PROXY protocol on inbound connections and use `realip` to
  recover the client address.
- `HTTP_PORT`/`HTTPS_PORT` (proxy-wide) and `EXTERNAL_HTTP_PORT`/`EXTERNAL_HTTPS_PORT` (per vhost) customize
  the external ports; the HTTPS-redirect target adjusts accordingly.

## DockYarp today
Kestrel listens on 8080/8443 with no PROXY-protocol support; ports are fixed in the listener wiring
(`src/DockYarp.App`) and image/compose. Client IP is derived from the transport connection only.

## Proposed change (sketch)
- PROXY protocol: enable Kestrel's PROXY-protocol support (or a connection middleware) on the edge listeners,
  gated by config, feeding the real client IP into forwarded headers.
- External ports: make the HTTP/HTTPS listener ports configurable and thread the external HTTPS port into the
  redirect target.

## Acceptance criteria (→ scenarios)
- **WHEN** PROXY protocol is enabled and a v1/v2 header precedes the connection **THEN** `X-Forwarded-For`/
  `X-Real-IP` carry the real client IP.
- **WHEN** custom listener ports are configured **THEN** the HTTP→HTTPS redirect targets the configured HTTPS
  port.
- **WHEN** PROXY protocol is disabled **THEN** connections without the header work normally.

## Notes / risks / references
- Confirm Kestrel PROXY-protocol options for the target .NET; validate in a Docker-capable session.
- Consider splitting external-port config into its own item if PROXY protocol slips.
