---
id: fix-e2e-ci-runner-timeout
capability: deployment
agent: AG-DEP
tier: B-runtime
priority: medium
status: backlog
nginx-proxy: n/a (internal test infrastructure)
provenance: real `add-e2e-release-gate` CI validation run, 2026-08-18 — https://github.com/gcelet/DockYARP/actions/runs/32186598665
---

## Why

`add-e2e-release-gate` wired the E2E suite into the release-publish gate (`DockerPublish.DependsOn(Test, E2E)`
in `build/Build.cs`), which is now confirmed working correctly: a real GitHub Actions run showed `E2E Failed` →
`Test NotRun` → `DockerPublish NotRun`, no image pushed. But the E2E suite itself failed **on the
GitHub-hosted runner specifically** — it passes reliably locally (every run this project's recent history,
including the same day). If this isn't addressed, **every release publish will fail** until it's fixed, since
the gate is (correctly) strict.

## Current state

Real failure, not assumed or reproduced locally (the failure is CI-runner-specific):
- `DockYarp.E2E.Tests\AspireAppHostFixture.cs:33`: `StartupTimeoutSeconds = 180` — the whole Aspire distributed
  system must finish booting within 180 seconds or the fixture's `CancellationTokenSource` fires.
- The system boots roughly 23 containers: 17 named backends (`BackendCatalog.All`,
  `tests/DockYarp.E2E.AppHost/BackendCatalog.cs`) plus `dockerproxy`, `ca-bundle`, `stepca`, `dockyarp`,
  `dockyarp-pp`, `acme-http01`.
- The real CI failure: `dockerproxy` finished waiting in ~6 seconds, but `dockyarp` itself never finished being
  created — `Failed to create resource dockyarp` at the 180s mark,
  `Aspire.Hosting.Dcp.ContainerCreator.BuildAndCreateContainerAsync` throwing `OperationCanceledException`
  (the fixture's own timeout, not a Docker-level error).
- GitHub-hosted `ubuntu-latest` standard runners are comparatively resource-constrained (2 vCPU / 7 GB RAM per
  GitHub's published specs) versus a typical local dev machine — the leading hypothesis is that booting ~23
  containers via DCP genuinely takes longer there, not that DCP is broken on CI.

## Proposed change (sketch)

Investigate before picking a fix — several plausible, non-exclusive options:
1. **Raise `StartupTimeoutSeconds`** — the cheapest change, if the real bottleneck is just "too little runway,"
   not a hang. Needs evidence it isn't masking an actual hang (e.g. does it eventually succeed if given more
   time, or does it truly never progress?).
2. **A larger/faster GitHub-hosted runner tier** (e.g. `ubuntu-latest-4-cores` or similar) for the `E2E` step
   specifically, if resource contention (not just wall-clock scheduling) is the bottleneck.
3. **Reduce what boots for the release-gate run specifically** — e.g. does every one of the 17 backends need
   to be part of a release-gate sanity check, or would a leaner subset (still exercising TLS/ACME/mTLS/routing
   meaningfully) boot reliably within budget while the full catalog stays for local/manual E2E runs?
4. Check whether `dockerproxy`'s fast start vs `dockyarp`'s stall points at something DockYarp-specific (e.g.
   its own container startup/health-check timing) rather than a generic "many containers" resource problem —
   don't assume it's purely about container *count* without checking.

## Acceptance criteria (→ scenarios)

- **WHEN** the release-gate `E2E` step runs on the actual GitHub Actions runner (not just locally) **THEN** it
  completes successfully within whatever timeout is settled on, reliably (not a one-off lucky pass).
- **WHEN** the fix is applied **THEN** a real `workflow_dispatch` run on `image.yml` (mirroring the run that
  found this) is used to confirm it, not just a local run — this bug was only ever visible on the real runner.

## Notes / risks / references

- Real failure data already exists: https://github.com/gcelet/DockYARP/actions/runs/32186598665 (job "Build &
  push image (release)", step "Test, E2E, and push (Nuke)") — read its full log before re-deriving anything.
- Until this is fixed, every real release-tag publish will fail at the gate — this blocks actually cutting a
  release, not just a nice-to-have. Higher urgency than a typical "medium" item once a real release is wanted,
  even though nothing is on fire today (edge publishes are unaffected, they skip the gate).
- `add-nondcp-e2e-harness` is unrelated to this — that item is about DCP's *architectural* inability to run
  certain network topologies at all, not about *timing* on a real runner.
