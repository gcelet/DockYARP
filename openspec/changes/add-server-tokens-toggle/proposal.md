## Why
DockYarp emits Kestrel's default `Server` response header, disclosing the server technology — a routine
hardening finding. nginx-proxy exposes `SERVER_TOKENS` to suppress or adjust it; DockYarp has no control over
the `Server` header today.

## What Changes
- Suppress the `Server` response header **by default** (disable Kestrel's built-in header).
- Add a `Security:ServerHeader` option: unset/empty → no header (default), a literal value → emit exactly that
  value.
- Emit the configured value from the existing security-headers middleware.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `security`: add control over the `Server` response header (suppressed by default; optional custom value).

## Impact
- **Code**: `src/DockYarp.App` (Kestrel `AddServerHeader = false`), `src/DockYarp.Security`
  (`SecurityHeadersOptions.ServerHeader` + `SecurityHeadersMiddleware`). Tests in
  `tests/DockYarp.Security.Tests` and `tests/DockYarp.IntegrationTests`.
- **Deferred**: per-host `Server` values (nginx `SERVER_TOKENS` is per-vhost) — folded into the future
  `vhost.d`-style overrides item.
- **Owning agent**: AG-SEC.
