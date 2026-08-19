---
id: reduce-ci-workflow-duplication
capability: deployment
agent: AG-DEP
tier: B-runtime
priority: low
status: backlog
nginx-proxy: n/a (internal CI efficiency)
provenance: 2026-08-19 user observation while triggering a workflow_dispatch on image.yml — a push to develop
  queues that run behind the same-push's auto-triggered publish-edge job (shared concurrency group), making
  the redundant work directly visible/annoying, not just theoretical
---

## Why

Every push to `develop` triggers **two** workflows that each do real, overlapping work: `ci.yml` (`./build.sh
Test` — restores + compiles the **whole solution**, then runs unit/integration tests) and `image.yml`'s
`publish-edge` job (`./build.sh DockerPublish --edge --skip-publish-gate ...`, which builds the Docker image —
the Dockerfile's build stage runs the Nuke `Publish` target, itself restoring + compiling `DockYarp.App`'s own
dependency graph, `.NET` build tools/NuGet restore all over again). Neither depends on or waits for the other.
This is real, measurable duplicated restore/compile work on every single trunk push, not just a theoretical
inefficiency — noticed directly while manually queuing a `workflow_dispatch` run behind an auto-triggered
`publish-edge` run sharing the same `image-${{ github.ref }}` concurrency group (they don't run in parallel
either, by design, compounding the wasted wall-clock time for anyone waiting on a manual dispatch).

## Current state

- `ci.yml`: `on: push: branches: [develop, main]` + `pull_request` — runs `Test` (full-solution restore +
  compile + unit/integration tests).
- `image.yml`'s `publish-edge` job: `on: push: branches: [develop]` (no tag) — runs `DockerPublish --edge
  --skip-publish-gate`, which builds the image via the Dockerfile (Nuke `Publish` target, `DockYarp.App`'s own
  graph only, already scoped down from a prior fix this session — not the *whole* solution, but still a real
  independent restore + compile of the overlapping subset).
- No caching or artifact-sharing between the two workflows; no ordering/dependency between them at all.

## Proposed change (sketch)

Not yet designed — needs real investigation before committing to an approach. Candidate directions, all with
real trade-offs to weigh (not a clear best pick yet):
1. **Make `publish-edge` depend on `ci.yml`'s success** (e.g. `workflow_run` trigger, or a reusable
   workflow/job dependency) — avoids building/pushing an edge image for code that doesn't even pass `Test`, at
   the cost of a genuinely sequential (slower wall-clock) pipeline instead of the current parallel one.
2. **Share a build/restore cache between the two workflows** (e.g. `actions/cache` keyed on
   `Directory.Packages.props`/`global.json`, or an intermediate build artifact) — keeps them parallel, cuts
   redundant NuGet restore time specifically, but doesn't eliminate redundant compilation itself.
3. **Accept the duplication as an acceptable trade-off for parallelism/speed** and only fix the *queuing*
   annoyance (the concurrency-group interaction that blocks a manual `workflow_dispatch` behind an unrelated
   auto-triggered edge push) — e.g. a narrower concurrency group so `workflow_dispatch` (manual testing/
   validation runs) doesn't queue behind ordinary `push`-triggered edge publishes.

## Acceptance criteria (→ scenarios)

- **WHEN** a push to `develop` triggers both workflows **THEN** whatever the chosen fix, redundant work is
  measurably reduced (restore time, compile time, or wall-clock queuing, depending on which direction is
  chosen) without weakening either workflow's own guarantees (`ci.yml`'s gate strength, `image.yml`'s edge
  channel staying fast).

## Notes / risks / references

- Genuinely low priority / not urgent — this is pure CI efficiency, nothing is broken, and mis-designing a fix
  here (e.g. option 1's added sequential latency) could make things worse in a different dimension. Don't rush
  a fix without weighing the trade-off explicitly at propose/design time.
- Distinct from `fix-e2e-ci-runner-timeout` (E2E-specific runner slowness) and `add-e2e-release-gate` (already
  shipped, the release-tag gate) — this is about ordinary trunk-push CI, not the release path.
