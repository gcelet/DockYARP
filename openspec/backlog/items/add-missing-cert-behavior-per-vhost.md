---
id: add-missing-cert-behavior-per-vhost
capability: security
agent: AG-SEC
tier: A-structural
priority: low
status: backlog
nginx-proxy: ENABLE_HTTP_ON_MISSING_CERT (per-vhost) / TRUST_DEFAULT_CERT (per-vhost label)
provenance: 2026-07-31 env-var compat pass (per-vhost overrides of global TLS-enforcement policies)
---

## Why
nginx-proxy's `ENABLE_HTTP_ON_MISSING_CERT` and `TRUST_DEFAULT_CERT` are **DUAL**: a global default and a
per-vhost override. DockYarp implements both **globally** (`Security:EnableHttpOnMissingCert`,
`Security:TrustDefaultCert`) but a single host cannot override them. This adds the per-host overrides,
completing the per-vhost TLS-enforcement family (alongside `HTTPS_METHOD`, `HSTS`, `SSL_POLICY`).

## nginx-proxy behavior
- `ENABLE_HTTP_ON_MISSING_CERT` (per-vhost, default true): serve HTTP (no redirect) when the vhost has no cert.
- `TRUST_DEFAULT_CERT` (global env; per-vhost via label `com.github.nginx-proxy.nginx-proxy.trust-default-cert`,
  default true): whether a vhost may fall back to the default certificate.

## DockYarp today
- Both are global `Security:*` options applied in `HttpsRedirectionMiddleware` (500 for an untrusted default,
  redirect suppression when no cert). No per-host override.

## Proposed change (sketch)
- Recognize per-container `ENABLE_HTTP_ON_MISSING_CERT` and `TRUST_DEFAULT_CERT` (env/label; the namespaced
  `com.github.nginx-proxy.nginx-proxy.trust-default-cert` is accepted as an alias for the latter). Parse as
  booleans, carry into the route's TLS metadata.
- In `HttpsRedirectionMiddleware`, the route's value takes precedence over the global default
  (`tls.X ?? options.X`).

## Acceptance criteria (→ scenarios)
- **WHEN** a host declares `TRUST_DEFAULT_CERT=false` while the global default is `true`, and an HTTPS request
  targets it with no real certificate **THEN** the request is refused with 500.
- **WHEN** a host declares `ENABLE_HTTP_ON_MISSING_CERT=false` while the global default is `true`, and the
  redirecting host has no certificate **THEN** it is redirected anyway.

## Notes / risks / references
- Small: parsing (bool) + two `HostTlsMetadata` fields + `tls.X ?? options.X` in the existing middleware.
  Unit-testable, no e2e. Accepts the namespaced `trust-default-cert` label deferred from `add-nginx-label-aliases`.
