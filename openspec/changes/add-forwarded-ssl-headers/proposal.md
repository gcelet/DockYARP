## Why
nginx-proxy always forwards two request headers DockYarp does not: `X-Forwarded-Ssl` (`on`/`off`) and
`X-Original-URI` (the original request URI). Backends that gate on TLS via `X-Forwarded-Ssl` (Rails
`assume_ssl`, some auth stacks) or reconstruct the original path via `X-Original-URI` (nginx
`auth_request`-style flows) behave differently behind DockYarp. Surfaced by the 2026-08-05 parity re-comparison.

## What Changes
- `ForwardedHeadersTransform` also sets, on every proxied request:
  - `X-Forwarded-Ssl`: `on` when the effective forwarded proto is HTTPS, else `off` — mirroring
    `X-Forwarded-Proto` so it respects the downstream-proxy trust setting (append vs replace).
  - `X-Original-URI`: the original request URI (`PathBase` + path + query), always the real URI (removed then
    set, so a client cannot spoof it).

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `yarp-dynamic-config`: the forwarded-header set gains `X-Forwarded-Ssl` and `X-Original-URI`.

## Impact
- **Code**: `DockYarp.App` — `ForwardedHeadersTransform` adds the two headers (derives SSL from the effective
  `X-Forwarded-Proto`; original URI from `HttpContext.Request`).
- **Tests (integration)**: `ForwardedHeadersIntegrationTests` — the backend receives `X-Forwarded-Ssl: off`
  over HTTP and an `X-Original-URI` carrying the original path + query.
- **Docs**: the site features page documents the two added forwarded headers (document-as-you-go).
- **Runtime / e2e**: none beyond the integration test (fully covered by the in-process proxy pipeline).
- **Owning agent**: AG-RP. Resolves `add-forwarded-ssl-headers`.
