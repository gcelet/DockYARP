---
id: add-renovate-gate-e2e-diagnostics
capability: deployment
agent: AG-DEP
tier: A-structural
priority: high
status: backlog
nginx-proxy: (internal finding — CI diagnostics, not a parity gap)
provenance: 2026-08-24 session — 7 Renovate PRs failing identically on
  `ProvisionedCertificate_IsReusedAfterRestart` (Aspire "failed to become healthy" after restart), does not
  reproduce locally against the same Aspire 13.5.2, and `renovate-gate.yml` has no way to recover the real
  per-resource container logs from a failing run to diagnose further
---

## Why
`.github/workflows/renovate-gate.yml`'s `full-gate` job runs the exact same `./build.sh Release` (Test+E2E+
DockerImage) gate as `image.yml`'s two jobs, but — unlike those — has **no "Upload E2E diagnostics" step**.
A real, currently-recurring E2E failure (`ProvisionedCertificate_IsReusedAfterRestart`, `Aspire.Hosting.
DistributedApplicationException: Stopped waiting for resource 'dockyarp' to become healthy because it failed
to start`) is blocking 7 open Renovate PRs identically, does not reproduce locally, and cannot be diagnosed
further from CI alone — only the pre-restart console tail is visible, never the post-restart container logs
that would show why `dockyarp` failed to start again.

## nginx-proxy behavior
N/A — internal CI diagnostics tooling, not a proxy-behavior parity gap.

## DockYarp today
`image.yml`'s `publish-release`/`publish-edge` jobs already upload `artifacts/e2e-logs/` as the `e2e-logs`
artifact on failure (added 2026-08-23, see the backlog-work-queue memory). `renovate-gate.yml`'s `full-gate`
job runs the identical Fallout `E2E` target (which writes to the same `artifacts/e2e-logs/` directory) but
never uploads it.

## Proposed change (sketch)
Add the same "Upload E2E diagnostics" step to `renovate-gate.yml`'s `full-gate` job, scoped the same way the
rest of that job's steps are (`if: github.event.pull_request.user.login == 'renovate[bot]'` combined with
`if: failure()`), mirroring `image.yml`'s existing step exactly.

## Acceptance criteria (→ scenarios)
- **WHEN** a Renovate PR's `full-gate` job fails **THEN** the `e2e-logs` artifact is uploaded (when
  `artifacts/e2e-logs/` has content) so per-resource container logs are recoverable after the runner tears
  down.
- **WHEN** a non-Renovate PR's `full-gate` job runs (all steps skipped, fast pass) **THEN** no artifact
  upload is attempted (nothing to upload, `if-no-files-found: ignore` keeps this silent).

## Notes / risks / references
- This unblocks diagnosing the actual `ProvisionedCertificate_IsReusedAfterRestart` root cause — a separate,
  not-yet-understood finding (see the backlog-work-queue memory's item 9) that this change does not fix by
  itself, only makes diagnosable.
