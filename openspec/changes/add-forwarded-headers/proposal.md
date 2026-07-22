## Why

Backends need to know the original client and scheme. nginx-proxy sets `X-Forwarded-*`, `X-Real-IP`, and a
correct `Host`, and gates trust of client-supplied forwarded headers (`TRUST_DOWNSTREAM_PROXY`). DockYarp
relies on YARP defaults without asserting them and sets no `X-Real-IP`; this makes the forwarding explicit.

## What Changes

- Explicitly configure forwarded headers on proxied requests: `X-Forwarded-For`, `X-Forwarded-Proto`,
  `X-Forwarded-Host`, `X-Forwarded-Port`, `X-Real-IP`, and preserve/normalize the `Host` header.
- Add a **trust option** (default: trust) controlling whether inbound client-supplied `X-Forwarded-*`
  headers are appended/preserved or replaced with the connection's own values (à la `TRUST_DOWNSTREAM_PROXY`).

## Capabilities

### Modified Capabilities
- `yarp-dynamic-config`: proxied requests carry explicit, configurable forwarded headers.

## Impact

- **Code**: `src/DockYarp.App/ReverseProxy` (YARP transforms / forwarded-headers configuration).
- **Owning agent**: AG-RP.
