---
id: add-default-cert-trust-toggle
capability: tls-acme
agent: AG-AT
tier: A-structural
priority: medium
status: backlog
nginx-proxy: default.crt / TRUST_DEFAULT_CERT / ENABLE_HTTP_ON_MISSING_CERT
provenance: this parity pass (matrix: default cert semantics ⛔)
---

## Why
nginx-proxy has explicit control over what happens when a vhost has no real certificate: use an
operator-supplied `default.crt`, refuse it (`TRUST_DEFAULT_CERT=false` → error), or force HTTP
(`ENABLE_HTTP_ON_MISSING_CERT`). DockYarp only has a self-signed fallback with no operator policy knobs.

## nginx-proxy behavior
- `default.crt`/`default.key`: certificate used when no per-host cert matches.
- `TRUST_DEFAULT_CERT` (default `true`): if `false`, vhosts without a real cert return a 500 instead of using
  `default.crt` (pre-1.7 behavior).
- `ENABLE_HTTP_ON_MISSING_CERT` (default `true`): when no cert exists at all, force `HTTPS_METHOD=noredirect`
  so the vhost stays reachable over HTTP.

## DockYarp today
A self-signed fallback cert is generated for unknown hosts (`src/DockYarp.Tls/DefaultCertificateProvider.cs`,
`DefaultCertificateFactory.cs`); redirect is already gated on real-cert availability
(`openspec/specs/security/spec.md`). No operator-supplied `default.crt`, no trust/deny toggle, no explicit
"force HTTP on missing cert" policy.

## Proposed change (sketch)
Add config to (a) prefer an operator-provided `default.crt`/`.key` over the generated self-signed cert,
(b) `TrustDefaultCert` toggle (deny → serve an error for hosts lacking a real cert), and (c) formalize the
"HTTP-on-missing-cert" policy alongside the existing redirect gating.

## Acceptance criteria (→ scenarios)
- **WHEN** `default.crt`/`.key` is provided **THEN** unknown hosts are served it instead of the self-signed
  cert.
- **WHEN** `TrustDefaultCert=false` and a host has no real cert **THEN** HTTPS to that host returns an error
  rather than the default cert.
- **WHEN** a host has no cert and HTTP-on-missing is enabled **THEN** it is reachable over HTTP (no forced
  HTTPS redirect).

## Notes / risks / references
- Reconcile with the existing self-signed fallback + redirect-gating so behaviors don't conflict.
