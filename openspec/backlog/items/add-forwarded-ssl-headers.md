---
id: add-forwarded-ssl-headers
capability: yarp-dynamic-config
agent: AG-RP
tier: A-structural
priority: medium
status: backlog
nginx-proxy: X-Forwarded-Ssl / X-Original-URI (proxy.conf)
provenance: 2026-08-05 parity re-comparison
---

## Why
nginx-proxy always forwards two request headers DockYarp does not: `X-Forwarded-Ssl` (`on`/`off`) and
`X-Original-URI` (the original request URI). Backends that gate on TLS via `X-Forwarded-Ssl` (Rails
`assume_ssl`, some auth stacks) or that reconstruct the original path via `X-Original-URI` (nginx
`auth_request`-style flows) behave differently behind DockYarp.

## nginx-proxy behavior
- `X-Forwarded-Ssl`: from the `$proxy_x_forwarded_ssl` map — `on` when the (effective) forwarded proto is
  `https`, else `off`.
- `X-Original-URI`: set to `$request_uri` (the original path + query, before any rewrite).
- Both are subject to the same downstream-proxy trust rules as the other `X-Forwarded-*` headers.

## DockYarp today
- `src/DockYarp.App/ReverseProxy/ForwardedHeadersTransform.cs` sets `X-Real-IP` and `X-Forwarded-Port` and
  relies on YARP defaults for `X-Forwarded-For`/`-Proto`/`-Host`. It sets **neither** `X-Forwarded-Ssl` nor
  `X-Original-URI`.

## Proposed change (sketch)
- In `ForwardedHeadersTransform`, add `X-Forwarded-Ssl = on|off` from the effective forwarded proto, and
  `X-Original-URI` from the original request path + query string.
- Honor the existing downstream-proxy trust mode (append vs replace) consistently with the other forwarded
  headers, so an untrusted client cannot spoof them.

## Acceptance criteria (→ scenarios)
- **WHEN** a request is proxied over HTTPS **THEN** the backend receives `X-Forwarded-Ssl: on`; over HTTP,
  `off`.
- **WHEN** a request is proxied **THEN** the backend receives `X-Original-URI` carrying the original path +
  query.
- **WHEN** downstream-proxy trust is off **THEN** both are derived from the real connection, not client input.

## Notes / risks / references
- Fully unit-testable (a transform test, like the existing forwarded-header tests) — a clean Windows-gate win.
- Sibling: the existing `X-Forwarded-*` handling (`ForwardedHeadersTransform`).
