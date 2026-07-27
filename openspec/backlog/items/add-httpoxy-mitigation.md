---
id: add-httpoxy-mitigation
capability: security
agent: AG-SEC
tier: A-structural
priority: low
status: backlog
nginx-proxy: Proxy request header stripping (httpoxy)
provenance: this parity pass (matrix: httpoxy mitigation ⛔)
---

## Why
nginx-proxy strips the inbound `Proxy` request header to mitigate the httpoxy vulnerability (CGI apps reading
`HTTP_PROXY` from it). DockYarp should not forward a client-supplied `Proxy` header to backends.

## nginx-proxy behavior
- The template unsets the `Proxy` header on every proxied request (httpoxy mitigation), alongside its default
  forwarded-header set.

## DockYarp today
Forwarded headers (`X-Forwarded-*`, `X-Real-IP`, Host) are handled
(`src/DockYarp.App/ReverseProxy/ForwardedHeadersTransform.cs`), but there is no explicit removal of an inbound
`Proxy` header (verify current transform behavior first).

## Proposed change (sketch)
Add a YARP request transform that removes the `Proxy` header before forwarding. Small, always-on. Add a test
asserting the header never reaches the backend.

## Acceptance criteria (→ scenarios)
- **WHEN** a client sends a `Proxy:` request header **THEN** the backend receives the request without a
  `Proxy` header.

## Notes / risks / references
- First confirm YARP doesn't already drop it; if it does, this becomes a documentation/test-only item.
