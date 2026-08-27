## Why

`AcmeHttpClient.SendSignedAsync` only special-cases one error type — `badNonce` (retried once, immediately) —
and ignores the CA's `Retry-After` header entirely, both on error responses and while polling an
authorization/order's status. Low-risk against step-ca (no default rate limits), but a real gap against Let's
Encrypt — the realistic default CA for most nginx-proxy-replacement operators (`add-acme-client-maintenance-
policy`'s audit; same reasoning as `add-acme-account-persistence`). Without it, a rate-limit response is
treated as an immediate, unretried failure, and status polling always waits a fixed 2s regardless of what the
CA itself suggests.

## What Changes

- `AcmeHttpClient.SendSignedAsync`'s existing single bounded retry (currently `badNonce`-only) also covers a
  `rateLimited` error (RFC 8555 §6.6's own example pairing) when the response carries a `Retry-After` header:
  the retry waits that long (capped — see `design.md`) before resending, instead of failing immediately. Other
  error types are unchanged — still fail immediately, since retrying wouldn't fix them.
- Authorization/order status polling (`WaitForValidationAsync`/`WaitForCertificateAsync` in `AcmeClient.cs`)
  uses a `Retry-After` value from the poll response when present, instead of the hardcoded 2s delay, falling
  back to 2s when absent.
- `Retry-After` parsing itself needs no new code: `HttpResponseHeaders.RetryAfter` already parses both RFC
  7231 forms (delay-seconds and HTTP-date) — a real implementation-time finding, simpler than the backlog
  item's own sketch assumed.

## Capabilities

### Modified Capabilities
- `tls-acme`: ACME error handling and status polling become `Retry-After`-aware where the CA provides one,
  instead of unconditionally fixed/immediate.

## Impact

- `src/DockYarp.Tls/Acme/AcmeHttpClient.cs`: `SendSignedAsync`'s retry condition and delay; `GetAuthorizationAsync`/
  `GetOrderAsync` need to also expose the poll response's `Retry-After` value alongside the parsed resource (a
  small return-type change, internal to this project — no external consumers).
- `src/DockYarp.Tls/AcmeClient.cs`: `WaitForValidationAsync`/`WaitForCertificateAsync` use the exposed
  `Retry-After` value for their poll delay.
- Tests: `tests/DockYarp.Tls.Tests/AcmeHttpClientTests.cs` (rate-limit retry honoring `Retry-After`, capped
  wait, other error types still failing immediately).
- Docs: `docs/tls-acme.md`'s "Client maintenance & security" section (closes this gap).
