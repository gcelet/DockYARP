---
id: add-server-tokens-toggle
capability: security
agent: AG-SEC
tier: A-structural
priority: low
status: done
nginx-proxy: SERVER_TOKENS
provenance: this parity pass (untracked feature)
---

## Why
nginx-proxy exposes `SERVER_TOKENS` to control the `Server` header (hide the version / suppress it) — a common
hardening step. DockYarp does not manage the `Server` header emitted by Kestrel.

## nginx-proxy behavior
- `SERVER_TOKENS` (per vhost) sets nginx `server_tokens` (`on`/`off`/`build`), controlling how much the
  `Server` header reveals (`nginx.tmpl:810,960`).

## DockYarp today
Kestrel emits its default `Server` header; no option to suppress/alter it. Security headers exist
(`src/DockYarp.Security/SecurityHeadersMiddleware.cs`, `SecurityHeadersOptions.cs`) but do not cover `Server`.

## Proposed change (sketch)
Disable Kestrel's `Server` header (`KestrelServerOptions.AddServerHeader = false`) and/or set a configurable
value via the security-headers middleware. Config-driven, default to hardened (suppressed).

## Acceptance criteria (→ scenarios)
- **WHEN** server tokens are suppressed **THEN** responses carry no `Server` header (or a generic value).
- **WHEN** a custom `Server` value is configured **THEN** responses carry it.

## Notes / risks / references
- Small; fold into the existing security-headers configuration surface.
