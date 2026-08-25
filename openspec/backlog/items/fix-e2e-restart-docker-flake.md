---
id: fix-e2e-restart-docker-flake
capability: deployment
agent: AG-DEP
tier: B-runtime
priority: high
status: backlog
nginx-proxy: (internal finding — E2E CI reliability, not a parity gap)
provenance: real e2e-logs diagnostics artifact downloaded from a failing Renovate PR run (2026-08-24/25),
  after add-renovate-gate-e2e-diagnostics unblocked recovering them — root-caused, not guessed
---

## Why
`ProvisionedCertificate_IsReusedAfterRestart` intermittently fails with `Aspire.Hosting.
DistributedApplicationException: Stopped waiting for resource 'dockyarp' to become healthy because it failed
to start` — confirmed on 2 of 6 simultaneously-rebased Renovate PRs, on totally unrelated package updates
(gRPC, postcss), ruling out an application-code regression. `[Retry(2)]` (added the prior evening for the
unrelated ACME-timeout flake) did not rescue these runs — the real `e2e-logs` diagnostics show only ONE
restart attempt across the whole run, not two.

## nginx-proxy behavior
N/A — internal E2E test infrastructure, not a proxy-behavior parity gap.

## DockYarp today
Root-caused via the real `Aspire.Hosting.Dcp.DcpExecutor.log` from a failing run (not guessed): Docker is
transiently slow to stop/delete the old `dockyarp` container on a loaded GitHub Actions runner — the log
shows several "Container 'dockyarp-qeadaqdn' is still running; trying again to stop it..." retries, then TWO
delete attempts that still reported the resource present before a third finally returned `NotFound`, then a
"Current log flush for dockyarp-qeadaqdn timed out after 00:00:05." Aspire's own `WaitForResourceHealthyAsync`
observes the resulting failed-to-start state and throws **immediately** rather than waiting out the caller's
token budget — so `RestartTimeoutSeconds` (180s) was never actually the bottleneck; increasing it would not
have helped.

`AspireAppHostFixture.RestartProxyAndWaitHealthyAsync` issued the restart command once with no resilience of
its own.

## Proposed change (sketch)
Already implemented: wrap the restart-command-then-wait-healthy sequence in
`RestartProxyAndWaitHealthyAsync` in a bounded retry loop (3 attempts, 5s delay between), catching
`Aspire.Hosting.DistributedApplicationException` specifically — re-issuing the restart command gives Docker's
earlier stop/delete cleanup time to actually finish before trying again, targeting the confirmed root cause
directly rather than relying on the outer NUnit `[Retry]` (which does not appear to re-invoke the restart
operation the way expected — a separate, not-yet-understood gap, not blocking this fix).

## Acceptance criteria (→ scenarios)
- **WHEN** the restart command's `WaitForResourceHealthyAsync` throws `DistributedApplicationException`
  **THEN** the restart command is reissued (up to 3 total attempts) instead of failing the test immediately.
- **WHEN** all 3 attempts fail **THEN** the exception still propagates — no silent masking of a genuine
  regression.
- **WHEN** a healthy local run exercises the restart **THEN** behavior is unchanged (first attempt succeeds,
  no extra delay).

## Notes / risks / references
- Does not explain why NUnit's `[Retry(2)]` on the test method itself did not produce a second observable
  restart attempt in the diagnostics — flagged as a separate, still-open curiosity, not chased further since
  this fix does not depend on it working.
- `[Retry(2)]` is left in place on the test method as an outer safety net.
