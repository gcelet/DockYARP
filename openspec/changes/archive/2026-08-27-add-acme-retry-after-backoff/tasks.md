## 1. Retry-After parsing and the capped constant (AG-AT)

- [x] 1.1 Extracted into its own `internal static class AcmeRetryAfter` (`Acme/AcmeRetryAfter.cs`) rather than
  private helpers on `AcmeHttpClient` — this is a real design improvement over the sketch: private methods
  can't be unit-tested even with `InternalsVisibleTo` (that only reaches `internal`), so a dedicated `internal
  static` class makes `Parse`/`Capped` directly, fast unit-testable without going through the request/retry
  pipeline. `Parse(HttpResponseHeaders) -> TimeSpan?` reads `headers.RetryAfter` (`Delta` when present,
  otherwise `Date - DateTimeOffset.UtcNow` clamped to a minimum of zero; `null` when absent). 6 new tests in
  `AcmeRetryAfterTests.cs` confirm `HttpResponseHeaders.RetryAfter` actually parses both forms (real behavior,
  not assumed).
- [x] 1.2 `internal static readonly TimeSpan Cap = TimeSpan.FromSeconds(60);` on `AcmeRetryAfter`, used by both
  `AcmeHttpClient`'s rate-limit retry and `AcmePollResult`'s polling delay via `AcmeRetryAfter.Capped(...)`.

## 2. Rate-limited retry (AG-AT)

- [x] 2.1 Extended `SendSignedAsync`'s bounded-retry condition: alongside `badNonce`, also retries once when
  `problem?.Type == RateLimitedProblemType` **and** `AcmeRetryAfter.Capped(response.Headers)` is non-null —
  in that case, awaits the capped duration before the loop's next iteration (`badNonce` still retries
  immediately, no wait). The `Retry-After` header is read from `response.Headers` **before** `response.Dispose()`
  (a real ordering detail the sketch didn't call out — disposing first would have made the header
  unreadable). Other error types, and `rateLimited` with no `Retry-After`, still throw immediately, unchanged.
- [x] 2.2 4 new unit tests in `AcmeHttpClientTests.cs`: (a) `rateLimited` + 1s `Retry-After` retries once and
  succeeds, with a `Stopwatch` assertion proving the delay was actually awaited (≥900ms elapsed), not skipped;
  (b) `rateLimited` with no `Retry-After` throws immediately, no retry; (c) a non-`rateLimited` error with
  `Retry-After` present still throws immediately; (d) the cap itself is proven fast/directly by
  `AcmeRetryAfterTests.Capped_ValueAboveCap_ReturnsTheCap` (task 1.1) rather than a slow 60s+ end-to-end test —
  a better fit than the sketch's "inject a fake clock/delay seam" suggestion, since the cap computation is
  already isolated in `AcmeRetryAfter` and needs no seam to test directly.

## 3. Status polling (AG-AT)

- [x] 3.1 Added `internal readonly record struct AcmePollResult<T>(T Resource, TimeSpan? RetryAfter);` in its
  own file (`Acme/AcmePollResult.cs`, matching this project's one-type-per-file convention). Changed
  `GetAuthorizationAsync`/`GetOrderAsync` to return `Task<AcmePollResult<AcmeAuthorization>>`/
  `Task<AcmePollResult<AcmeOrder>>` via a new `SendSignedForPollAsync` (parallel to `SendSignedForJsonAsync`,
  additionally capturing `AcmeRetryAfter.Capped(response.Headers)` — kept as its own method rather than
  changing `SendSignedForJsonAsync` itself, since `FinalizeOrderAsync` also uses that helper and doesn't need
  `Retry-After`).
- [x] 3.2 Updated `AcmeClient.cs`: the initial `GetAuthorizationAsync` call in `RequestCertificateAsync` now
  unwraps `.Resource`; `WaitForValidationAsync`/`WaitForCertificateAsync` use `poll.RetryAfter ?? DefaultPollDelay`
  (a new named constant replacing the inline `TimeSpan.FromSeconds(2)` literal) as the delay before the next
  poll attempt.
- [x] 3.3 2 new unit tests in `AcmeHttpClientTests.cs`: `GetAuthorization_SurfacesRetryAfterFromTheResponse`
  and `GetAuthorization_WithNoRetryAfter_SurfacesNull`, calling `GetAuthorizationAsync` directly (the level
  actually testable — `AcmeClient` itself remains integration-only per its existing class remark).

## 4. Docs (AG-AT)

- [x] 4.1 Replaced `docs/tls-acme.md`'s Retry-After gap bullet with a resolved "Retry-After-aware backoff"
  paragraph, linking to this change and noting the scope decision (rateLimited only, capped at 60s).

## 5. Verification

- [x] 5.1 `dotnet build DockYarp.slnx`: 0 errors (only the pre-existing, unrelated ASPIRE010 warning).
  `dotnet test DockYarp.slnx --filter "TestCategory!=EndToEnd"`: 41+151+51+126+145 = 514 tests, 0 failures
  (`DockYarp.Tls.Tests` +12 new: 7 `AcmeRetryAfterTests`, 5 `AcmeHttpClientTests`).
- [x] 5.2 `openspec validate add-acme-retry-after-backoff --strict` passes.
