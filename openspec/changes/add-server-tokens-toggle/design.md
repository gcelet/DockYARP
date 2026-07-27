## Context
Kestrel adds `Server: Kestrel` to every response by default. Baseline response headers are already applied by
`SecurityHeadersMiddleware` (`src/DockYarp.Security/SecurityHeadersMiddleware.cs`), configured via
`SecurityHeadersOptions` (`src/DockYarp.Security/SecurityHeadersOptions.cs`). No `Server`-header handling exists.

## Goals / Non-Goals
- **Goal**: suppress the `Server` header by default; allow an operator-configured custom value.
- **Non-Goal**: per-host `Server` values. nginx-proxy's `SERVER_TOKENS` is per-vhost; DockYarp does this
  globally for now (per-host can arrive with the future `vhost.d`-style overrides).

## Decisions
- Disable Kestrel's built-in header: `KestrelServerOptions.AddServerHeader = false` in the App host. This
  removes the default `Server: Kestrel` regardless of the option below.
- Add `SecurityHeadersOptions.ServerHeader` (nullable string). When null/empty (default), no `Server` header is
  emitted. When non-empty, `SecurityHeadersMiddleware` sets `Server` to that value.
- Emitting from the middleware (not a second Kestrel knob) keeps all response-header policy in one place.

## Risks / Trade-offs
- Behavior change: the `Server` header disappears by default. This is the intended hardening; documented in
  `docs/security-middleware.md`.
- Global, not per-host — accepted for now (see Non-Goals).

## Migration Plan
- No config migration required; the default simply stops advertising `Server`. Operators wanting a value set
  `Security:ServerHeader`.

## Open Questions
- None.
