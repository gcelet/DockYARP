## Context

See `proposal.md` - Why. `AcmeHttpClient.SendSignedAsync` (`src/DockYarp.Tls/Acme/AcmeHttpClient.cs`) currently
loops at most twice (`attempt < 2`): on a non-2xx response it inspects the problem body, retries immediately
and only for `badNonce` (`BadNonceProblemType`), otherwise throws right away. `AcmeClient.WaitForValidationAsync`/
`WaitForCertificateAsync` (`src/DockYarp.Tls/AcmeClient.cs`) poll in a 30-attempt loop with a hardcoded
`TimeSpan.FromSeconds(2)` delay between attempts, via `GetAuthorizationAsync`/`GetOrderAsync`
(`SendSignedForJsonAsync`), which currently discard response headers and return only the parsed body.

## Goals / Non-Goals

**Goals:**
- Honor a CA's `Retry-After` guidance on a `rateLimited` error and during status polling, bounded so it can't
  stall a provisioning attempt indefinitely.

**Non-Goals:**
- Retrying on error types other than `rateLimited` — RFC 8555 §6.6 is the one place the spec explicitly pairs
  `Retry-After` with an error type; broadening to "any error with a `Retry-After` header" would retry errors
  retrying can't fix (`malformed`, `unauthorized`, ...), which is worse than today's immediate failure, not
  better.
- Exponential backoff or a multi-attempt retry policy — one bounded retry on `rateLimited`, matching the
  existing `badNonce` retry's own shape. A `rateLimited` failure that persists past that one retry still
  propagates to `CertificateProvisioningService`'s existing per-host failure logging/retry-on-next-reconcile-
  pass mechanism — already the real "try again later" path in this codebase, not something this change needs
  to duplicate.
- Hand-rolling RFC 7231 `Retry-After` parsing — `System.Net.Http.Headers.HttpResponseHeaders.RetryAfter` is a
  `RetryConditionHeaderValue?` that already parses both forms (`Delta`/`Date`); confirmed against real .NET
  behavior via a unit test (not assumed) as part of this change.

## Decisions

**Scope: `rateLimited` only, not a broader error-type match.** See Non-Goals above.

**Capped wait, one bounded maximum for both uses.** Neither an adversarial nor a misconfigured CA should be
able to stall a reconcile-pass slot far beyond this codebase's existing time scales (the polling loop's own
existing ceiling is `30 × 2s = 60s`). A single constant cap — 60 seconds — applies both to the rate-limit retry
wait and to a polling `Retry-After` value: simpler than two different caps, and 60s is already the order of
magnitude this code operates at. A `Retry-After` below the cap is honored as-is; above it, the cap is used
instead. This does not defeat honoring the CA's guidance in the common case — real CAs (step-ca, Let's Encrypt)
send small `Retry-After` values (seconds) for both rate-limiting and polling; the cap only bites an unusually
large value.

**`AcmeHttpClient` exposes `Retry-After` alongside the parsed poll resource.** `GetAuthorizationAsync`/
`GetOrderAsync` change from returning `Task<AcmeAuthorization>`/`Task<AcmeOrder>` to a small
`internal readonly record struct AcmePollResult<T>(T Resource, TimeSpan? RetryAfter)`, mirroring existing small
record-struct patterns in this file (`AcmeOrderCreated`, `AcmeJws.JwsRequestContext`). Both methods are
`internal`, consumed only by `AcmeClient` in the same assembly — a pure internal signature change, no public
API impact.

## Risks / Trade-offs

- **[Risk]** Honoring an CA-suggested wait inside `SendSignedAsync`'s retry occupies one of
  `CertificateProvisioningService`'s `MaxConcurrentProvisions` (8) concurrent slots for up to the cap's
  duration. **Mitigation**: bounded by the same 60s cap as polling; the existing "one slow host doesn't block
  others" guarantee still holds structurally (bounded concurrency, not a global lock) — it only reduces
  available concurrency slightly for that duration, the same trade-off already accepted for a slow HTTP-01
  validation today.
- **[Risk]** A CA that returns `Retry-After` as an HTTP-date already in the past (clock skew, or a
  fast-expiring hint) would resolve to a zero or negative wait. **Mitigation**: clamp the computed duration to
  a minimum of `TimeSpan.Zero` — proceeds immediately rather than throwing or waiting a negative time.

## Migration Plan

Purely additive — no configuration, no persisted state, no behavior change when a CA doesn't send
`Retry-After` (the common case for `badNonce` and every other error type today). Safe to roll back: reverting
restores the previous immediate-fail/fixed-2s-poll behavior exactly.
