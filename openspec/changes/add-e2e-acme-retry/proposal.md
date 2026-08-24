## Why

Three ACME HTTP-01 E2E provisioning tests intermittently fail with `System.TimeoutException: Timed out
waiting for ACME authorization` (`CertesAcmeClient.WaitForValidationAsync`, 30 attempts × 2s). This has hit
twice in two days on two unrelated changes — a real `develop` push (2026-08-23) and a Renovate PR that only
pins Docker base-image digests (2026-08-24) — ruling out an application-code regression as the cause both
times. The first occurrence was independently confirmed as a genuine CI-runner-level flake (two clean full
E2E reruns passed 42/42). The recurring cost is that this transient, unrelated condition blocks `Test`/`E2E`
in `build/Build.cs`, and therefore `DockerPublish`/Renovate `automerge`, on every CI run it touches.

## What Changes

- Add a scoped NUnit `[Retry(N)]` (small N) to exactly the 3 ACME HTTP-01 provisioning E2E tests that exhibit
  this pattern, not the E2E suite as a whole.
- Leave the existing e2e-logs diagnostics upload (`artifacts/e2e-logs/`) untouched, so an all-attempts-failed
  run remains fully diagnosable.
- No change to the 60s ACME authorization poll budget itself.

## Capabilities

Pure test-infrastructure resilience change — no product-facing behavior changes. `skip_specs: true` is set in
this change's `.openspec.yaml` (no capability deltas).

### New Capabilities
(none)

### Modified Capabilities
(none)

## Impact

- `tests/DockYarp.E2E.Tests` — the 3 affected test methods (ACME HTTP-01 provisioning: reuse-after-restart,
  chain-includes-intermediate, provisioned-for-host).
- No changes to `src/DockYarp.Tls` or any other source project.
- `docs/testing.md`'s e2e coverage map does not need updating — no test is added or removed, only made more
  resilient to CI flakiness.
