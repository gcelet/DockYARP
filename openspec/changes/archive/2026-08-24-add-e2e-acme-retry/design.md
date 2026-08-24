## Context

The 3 affected tests (`AcmeCertificate_IsProvisionedForHost`, `AcmeCertificate_ChainIncludesIntermediate` in
`TlsTests.cs`; `ProvisionedCertificate_IsReusedAfterRestart` in `RestartPersistenceTests.cs`) all derive from
`E2ETestBase` and poll via the shared `PollAsync` helper with a 60s budget (`TlsPollSeconds`). The Aspire
AppHost fixture (`AspireAppHostFixture`) is started once per test run (`[SetUpFixture]`/`OneTimeSetUp`), not
per test — so a retried test method reuses the already-running containers, it does not restart them.

Critically, `DockYarp.Tls.CertificateProvisioningService` (a `BackgroundService`) runs its own reconciliation
loop with `Tls__CheckInterval` overridden to **5 seconds** in the E2E AppHost (`Program.cs`, "retry
provisioning after discovery"), versus the 12h production default. Each `ReconcileAsync` pass has its own
internal ~60s ACME-authorization poll budget (`CertesAcmeClient.WaitForValidationAsync`, 30×2s). So when a
single reconciliation pass hits a transient step-ca hiccup and its internal budget is exhausted, the
background service tries again 5s later — but that next full attempt can itself take up to ~60s, which can
run past the *test's own* 60s `PollAsync` window. The test and the app's retry loop are racing two independent
timeouts that are not sized to nest cleanly.

See `proposal.md` for the motivating flake history (two occurrences in two days, both traced to CI-runner
timing, not application code).

## Goals / Non-Goals

**Goals:**
- Give the 3 affected tests a real chance to observe a provisioning success that the app's own background
  retry loop produces slightly outside a single 60s test poll window.
- Keep the fix cheap: no full AppHost/container restart per retry, no change to production defaults.

**Non-Goals:**
- Not changing `TlsPollSeconds`, `CertesAcmeClient`'s internal poll budget, or `Tls__CheckInterval` — those
  govern real (production and E2E-tuned) timing behavior, not test resilience.
- Not adding `[Retry]` to any other E2E test — only the 3 with this specific, confirmed failure signature.
- Not masking a real regression: if the same 3 tests still fail after retries, the run must still report
  failure.

## Decisions

**NUnit `[Retry(2)]` on exactly the 3 affected test methods**, not a suite-wide or fixture-level setting.
Rationale: because the AppHost fixture is shared (`OneTimeSetUp`) and the background reconciliation loop keeps
running across test invocations, a retried test's fresh `PollAsync` call is not a blind hope — it lines up
with the app's own 5s retry cadence and gives a second ~60s window a real chance to catch a reconciliation
pass that started after the first window's deadline. This is cheap: no container/AppHost restart, only the
test method body (an HTTP poll loop) re-runs.

**Alternative considered — raise `TlsPollSeconds`**: rejected. Enlarging the single-attempt window doesn't fix
the underlying two-nested-timeout race (a wider test window can still lose to a reconciliation pass that
itself takes longer), and it would slow down every green run's worst case, not just the rare flake.

**Alternative considered — reduce `CertesAcmeClient`'s internal timeout so more attempts fit per test
window**: rejected as out of scope — that budget is production-facing behavior (real ACME CAs can legitimately
take longer than a fast local step-ca), and changing it belongs to a TLS-behavior change, not a test-resilience
one.

## Risks / Trade-offs

- [Risk] `[Retry]` could mask a genuine future regression in ACME provisioning timing → Mitigation: bounded to
  N=2 (not unbounded), scoped to exactly 3 known tests, and the existing e2e-logs diagnostics upload still
  captures the final failing attempt's logs for investigation if all retries are exhausted.
- [Risk] A retried test still shares mutable proxy/cert state with the first attempt (same running app) →
  Mitigation: not a real risk here — each test targets its own distinct host as before; retrying the same
  test method against the same host converges toward the same expected end state, it does not compound.
