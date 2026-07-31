---
id: add-server-tokens-toggle
capability: security
agent: AG-SEC
tier: A-structural
priority: low
status: backlog
nginx-proxy: SERVER_TOKENS (per-vhost)
provenance: 2026-07-31 env-var compat pass
---

## Why
nginx-proxy's `SERVER_TOKENS` is a **per-vhost** knob controlling the `Server` response header. DockYarp
suppresses the `Server` header globally (Kestrel `AddServerHeader=false`) and can emit a configured value via
`Security:ServerHeader`, but a single host cannot opt out of that global value. This closes the per-container
gap so a host can suppress its `Server` header independently.

## nginx-proxy behavior
- `SERVER_TOKENS` (per-vhost, default empty) sets nginx's `server_tokens` directive (`on`/`off`/`build`),
  controlling whether the server software/version appears in the `Server` header and error pages.

## DockYarp today
- The `Server` header is suppressed by default; `Security:ServerHeader` (global) emits a custom value
  (`SecurityHeadersMiddleware`). No per-host override.

## Proposed change (sketch)
- Recognize a per-container `SERVER_TOKENS` (env/label, env wins). Carry it as a top-level `RouteRule` field
  (applies to HTTP and HTTPS responses, so not in `HostTlsMetadata`).
- In `SecurityHeadersMiddleware`, a per-host `SERVER_TOKENS=off` (or empty) suppresses the `Server` header for
  that host, overriding the global `Security:ServerHeader`; any other value keeps the global behavior. DockYarp
  has no server version to reveal, so nginx's `on`/`build` map to "use the global value".

## Acceptance criteria (→ scenarios)
- **WHEN** a global `Security:ServerHeader` is set and a host declares `SERVER_TOKENS=off`
- **THEN** that host's responses carry no `Server` header, while other hosts still emit the global value.
- **WHEN** a host declares no `SERVER_TOKENS` **THEN** the global `Server` behavior applies.

## Notes / risks / references
- Small: parsing + one top-level route field + a guard in the existing middleware. Unit-testable (no Docker,
  no e2e). Mirrors the per-host `HSTS=off` suppression already in `SecurityHeadersMiddleware`.
