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

## Investigation log (2026-08-19)

**Local reproduction attempted, twice, both negative** — genuinely tried before assuming, not skipped:
1. Cold external-image cache only (`smallstep/step-ca`, `alpine`, `alpine/socat`,
   `tecnativa/docker-socket-proxy`, `traefik/whoami`, both `aspnet:10.0` variants removed, normal Docker
   Desktop resources): `./build.ps1 E2E` succeeded in 1:50 total.
2. CPU/RAM constrained to match GitHub's published `ubuntu-latest` spec exactly (2 vCPU, ~6.8 GB via a
   temporary `.wslconfig`, Docker Desktop fully restarted to apply it, restored afterward — confirmed via
   `docker info` before/after): warm cache → 1:24; **cold cache + constrained together (closest local proxy for
   the real CI environment)** → 1:26.

None of these reproduced anything close to the real failure. This rules out "generic resource/cache
constraints matching GitHub's published specs" as sufficient on their own — something else about the actual
runner environment (network path/registry throttling specific to GitHub's infra, or per-core compute
characteristics not captured by a raw vCPU count, or disk I/O) is the more likely remaining explanation.

**Second real CI run, same-shape failure — ruled out "one-off fluke"**: a second `workflow_dispatch` run
(https://github.com/gcelet/DockYARP/actions/runs/32292201589, no code changes from the first run) failed the
same way: `dockerproxy` ready quickly, then `dockyarp`/`ca-bundle` never completing, `Failed to create resource
dockyarp` after ~169–180s. Two independent real runs, same failure point, same rough timing → systematic, not
transient.

**Update — real root cause found (2026-08-19, round 2)**: the timeout bump (180s → 420s) + artifact upload were
implemented and pushed. A third real run (workflow_dispatch,
https://github.com/gcelet/DockYARP/actions/runs/32293946454) **still failed** — the timeout bump alone was not
the fix (stalled at ~407s of the new 420s budget). But the artifact upload worked, and `stepca.log` finally
revealed the real cause:
```
/entrypoint.sh: line 59: /home/step/password: Permission denied
```
`step-ca`'s own container cannot write into its bind-mounted `/home/step` (`E2EPaths.StepCaDirectory`) on a
native Linux runner — **deterministic, not a timing issue**. Invisible on Windows/Docker Desktop because its
WSL2 bind-mount translation is permissive regardless of the container's internal UID, which is exactly why
both local reproduction attempts (cold cache, matched CPU/RAM) never surfaced it despite genuine effort. Same
class of bug `TlsHarness.PrepareCertsDirectory()` already fixes for `E2EPaths.CertsDirectory`
(`File.SetUnixFileMode(..., worldWritable)`) — just never applied to `StepCaDirectory`. Fixed by applying the
identical pattern. Validation round 2 (confirming this actual fix) pending.
