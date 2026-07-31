---
id: add-acme-challenge-options
capability: tls-acme
agent: AG-AT
tier: B-runtime
priority: low
status: backlog
nginx-proxy: ACME_HTTP_CHALLENGE_LOCATION / ACME_HTTP_CHALLENGE_ACCEPT_UNKNOWN_HOST
provenance: 2026-07-31 parity re-analysis
---

## Why
nginx-proxy exposes knobs governing how the ACME HTTP-01 challenge location is served:
`ACME_HTTP_CHALLENGE_LOCATION` (`true`/`false`/`legacy`) and `ACME_HTTP_CHALLENGE_ACCEPT_UNKNOWN_HOST`. DockYarp
serves HTTP-01 challenges but exposes no toggle for enabling/disabling the location or accepting challenges for
unknown/not-yet-routed hosts.

## nginx-proxy behavior
- `ACME_HTTP_CHALLENGE_LOCATION` (default `true`): enable/disable/`legacy` handling of the challenge location.
- `ACME_HTTP_CHALLENGE_ACCEPT_UNKNOWN_HOST` (default `false`): answer challenges for hosts with no matching
  running vhost (useful for provisioning before the backend exists).
  (Note: ACME issuance itself is the separate acme-companion; nginx-proxy only serves the challenge path.)

## DockYarp today
- `Http01ChallengeMiddleware`/`Http01ChallengeStore` serve the challenge; always on, no accept-unknown-host
  option (see `openspec/specs/tls-acme/spec.md`).

## Proposed change (sketch)
- Add options to disable the challenge location and to accept challenges for unknown hosts (serve the token
  even when no route matches the host). Keep the default behavior unchanged.

## Acceptance criteria (→ scenarios)
- **WHEN** accept-unknown-host is enabled **THEN** an HTTP-01 challenge for a host with no route is still
  answered from the token store.
- **WHEN** the challenge location is disabled **THEN** the challenge path returns 404.

## Notes / risks / references
- Small options extension over the existing challenge middleware. Runtime/e2e validation for the unknown-host
  path fits a Docker/ACME e2e session.
