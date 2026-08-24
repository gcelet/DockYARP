---
id: add-e2e-acme-retry
capability: tls-acme
agent: AG-AT
tier: B-runtime
priority: medium
status: backlog
nginx-proxy: (internal finding — E2E CI reliability, not a parity gap)
provenance: two independent occurrences of the same failure signature within 2 days (2026-08-23 develop CI
  run; 2026-08-24 Renovate PR #2 `renovate/pin-dependencies`, a Docker-digest-only change unrelated to
  ACME/Certes code) — see the "Fallout DependsOn eager evaluation" / CI-investigation memory for the first
  occurrence's full diagnosis
---

## Why
Three ACME HTTP-01 E2E provisioning tests (`ProvisionedCertificate_IsReusedAfterRestart`,
`AcmeCertificate_ChainIncludesIntermediate`, `AcmeCertificate_IsProvisionedForHost` — host names vary run to
run, e.g. `http1.local`/`modern.local`/`mtls-optional.local`) intermittently fail with
`System.TimeoutException: Timed out waiting for ACME authorization` from
`CertesAcmeClient.WaitForValidationAsync` (60s poll budget: 30 attempts × 2s). This has now been seen twice in
two days, on two unrelated changes (2026-08-23: a real develop push; 2026-08-24: a Renovate PR that only pins
Docker base-image digests, ruling out an application-code regression as the cause both times). The 2026-08-23
occurrence was independently verified as a genuine CI-runner-level flake (two clean full E2E reruns passed
42/42), so this is not about fixing a real bug — it's about the E2E suite's own resilience to that flake
recurring, since it currently blocks Renovate's `automerge` (and any human PR) on a transient, unrelated
condition.

## nginx-proxy behavior
N/A — this is DockYarp-internal CI/test infrastructure, not a proxy-behavior parity gap.

## DockYarp today
The 3 affected tests live in `tests/DockYarp.E2E.Tests` and run no NUnit retry policy — a single transient
runner-level hiccup (container scheduling delay, ACME challenge-server timing) fails the whole E2E run,
blocking `Test`/`E2E` in `build/Build.cs` and therefore `DockerPublish`/`automerge` on CI, with no automatic
recovery.

## Proposed change (sketch)
Add a scoped NUnit `[Retry(N)]` (small N, e.g. 2) to exactly the ACME HTTP-01 provisioning tests that exhibit
this pattern — not suite-wide — so a single transient authorization-polling timeout is retried before being
reported as a failure. Keep the existing e2e-logs diagnostics upload (`artifacts/e2e-logs/`) intact so a
still-failing (all-attempts-exhausted) run remains fully diagnosable. Do not increase the 60s poll budget
itself unless the retry data later suggests the timeout is systematically too tight rather than a runner
hiccup.

## Acceptance criteria (→ scenarios)
- **WHEN** an ACME HTTP-01 provisioning E2E test hits a transient `TimeoutException` waiting for ACME
  authorization **THEN** it is retried (bounded, small N) before being reported as a failure.
- **WHEN** all retry attempts fail **THEN** the test still fails and reports normally — no silent masking of a
  real regression.
- **WHEN** the retry is exercised **THEN** it is scoped to the specific ACME HTTP-01 tests, not applied
  suite-wide.

## Notes / risks / references
- Do not treat this as a substitute for investigating a *third* occurrence — if the same signature recurs
  after this change ships, that would point at a real timing regression (e.g. from `investigate-aot-build`
  changes) rather than a runner flake, and should be reopened as its own finding.
- `docs/testing.md`'s e2e coverage map does not need updating — no test is added or removed, only made more
  resilient to CI flakiness.
