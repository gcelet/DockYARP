---
id: add-acme-retry-after-backoff
capability: tls-acme
agent: AG-AT
tier: A-structural
priority: medium
status: backlog
nginx-proxy: n/a (internal finding — client-side protocol robustness, not a parity gap)
provenance: 2026-08-27 add-acme-client-maintenance-policy's real RFC 8555 completeness audit of AcmeClient,
  re-confirmed worth a real item (not just a doc note) after user pushback on the original severity
  assessment — same Let's Encrypt-vs-step-ca reasoning as add-acme-account-persistence
---

## Why

`AcmeHttpClient`'s `SendSignedAsync` only special-cases one error type — `badNonce` (RFC 8555 §6.7), retried
once with a fresh nonce. It ignores the `Retry-After` HTTP header entirely, which a CA may return both on
rate-limit errors and on authorization/order status-polling responses to hint how long to wait before the
next attempt. Low-risk against step-ca (no default rate limits), but a real gap against Let's Encrypt — the
realistic default CA for most nginx-proxy-replacement operators (see `add-acme-account-persistence` for the
fuller reasoning on why step-ca-shaped assumptions under-weighted this doc's original severity assessment).
Without it, a real rate-limit event or CA-suggested poll interval is simply ignored — DockYarp keeps
retrying/polling on its own hardcoded cadence (a fixed 2s for authorization/order polling today) regardless
of what the CA actually asked for.

## nginx-proxy behavior

N/A — internal client-side protocol robustness, not a proxy-behavior parity gap.

## DockYarp today

`src/DockYarp.Tls/Acme/AcmeHttpClient.cs`'s `SendSignedAsync` reads the `Replay-Nonce` response header but
never reads `Retry-After`. `AcmeClient.cs`'s `WaitForValidationAsync`/`WaitForCertificateAsync` poll on a
fixed `TimeSpan.FromSeconds(2)` regardless of anything the CA's own response might suggest.

## Proposed change (sketch)

1. Parse the `Retry-After` header (RFC 7231 §7.1.3 format — either a delay in seconds or an HTTP-date) when
   present on a response, in `AcmeHttpClient`.
2. On a rate-limit-flavored error (`urn:ietf:params:acme:error:rateLimited`, and plausibly other transient
   `5xx`/retryable errors — decide the exact scope, don't assume "all errors" is right), honor the CA's
   `Retry-After` value instead of failing immediately (currently every non-`badNonce` error throws right
   away, no retry at all).
3. On authorization/order status-polling responses, use a returned `Retry-After` as the next poll delay
   instead of the hardcoded 2s, when the CA provides one — fall back to 2s when it doesn't.

## Acceptance criteria (→ scenarios)

TBD — depends on the exact error-scope decision above. At minimum:
- **WHEN** a CA response includes a `Retry-After` header **THEN** DockYarp waits at least that long before
  its next attempt, instead of a fixed hardcoded delay.
- **WHEN** no `Retry-After` header is present **THEN** behavior is unchanged (existing fixed delays/bounded
  retries still apply) — this is additive, not a replacement of the existing `badNonce` handling.

## Notes / risks / references

- Refs: `docs/tls-acme.md`'s "Client maintenance & security" section, `add-acme-account-persistence` (the
  higher-priority sibling gap from the same audit, same Let's Encrypt-realism reasoning).
