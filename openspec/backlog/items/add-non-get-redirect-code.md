---
id: add-non-get-redirect-code
capability: security
agent: AG-SEC
tier: A-structural
priority: low
status: backlog
nginx-proxy: NON_GET_REDIRECT
provenance: this parity pass (untracked feature)
---

## Why
When redirecting HTTP→HTTPS, a `301`/`302` on a non-GET request makes clients replay it as GET, losing the
body/method. nginx-proxy's `NON_GET_REDIRECT` lets operators use `307`/`308` to preserve the method. DockYarp
uses a single redirect code.

## nginx-proxy behavior
- `NON_GET_REDIRECT` (per proxy or per container; `307`/`308`, default `301`) sets the redirect status used for
  non-GET methods on the HTTP→HTTPS redirect.

## DockYarp today
HTTPS redirection is in `src/DockYarp.Security/HttpsRedirectionMiddleware.cs` with a fixed redirect status; no
method-aware code selection.

## Proposed change (sketch)
Make the redirect status method-aware: keep the current code for GET/HEAD and use a configurable `307`/`308`
for other methods. Config-driven, default preserving current behavior for GET.

## Acceptance criteria (→ scenarios)
- **WHEN** a POST hits an HTTP endpoint under `HTTPS_METHOD=redirect` and non-GET redirect is `308` **THEN**
  the client receives a 308 to the HTTPS URL (method/body preserved).
- **WHEN** a GET hits the same endpoint **THEN** the existing redirect code is used.

## Notes / risks / references
- Small middleware change + tests.
