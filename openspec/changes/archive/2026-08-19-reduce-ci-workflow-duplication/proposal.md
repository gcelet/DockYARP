## Why

Every push to `develop` triggers two workflows that each do real, overlapping work: `ci.yml` (full-solution
restore + compile + unit/integration tests) and `image.yml`'s `publish-edge` job (its own independent restore +
compile of `DockYarp.App`'s dependency graph, inside the Dockerfile's build stage). Neither depends on the
other, and they don't even run in parallel when both are active on the same ref — `image.yml`'s
`image-${{ github.ref }}` concurrency group queues a manual `workflow_dispatch` behind an auto-triggered
`publish-edge` run for the same push, which is what made the redundant work directly visible and annoying
(not just a theoretical inefficiency) while validating `fix-e2e-ci-runner-timeout` this session.

## What Changes

- **`image.yml`**: narrow the `publish-edge` job's concurrency group so a manual `workflow_dispatch` run no
  longer queues behind an unrelated `push`-triggered edge publish on the same ref — the two request types stop
  contending for the same slot.
- **`ci.yml` and `image.yml`**: add a shared NuGet restore cache (`actions/cache`, keyed on
  `Directory.Packages.props` + `global.json`) so the two independent restores on the same push reuse packages
  instead of re-downloading them, cutting the redundant portion of the duplicated work without introducing a
  sequential dependency between the workflows (both keep running in parallel; compilation itself still happens
  twice, an accepted trade-off — see design.md).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None — this changes CI *implementation* (caching, concurrency-group scoping), not any spec-level behavior:
`ci.yml` still runs the `Test` gate on every push/PR exactly as `openspec/specs/deployment/spec.md`'s
"Continuous integration" requirement describes, and `image.yml` still publishes the `edge` channel ungated,
per "Continuous image publishing". `.openspec.yaml` sets `skip_specs: true` accordingly.

## Impact

- `.github/workflows/ci.yml` — add a restore-cache step.
- `.github/workflows/image.yml` — add a restore-cache step (mirroring `ci.yml`'s cache key so both workflows
  can share cache entries); narrow the `publish-edge` job's `concurrency.group`.
- No application code, no `build/Build.cs` changes — this is CI-workflow-only, orchestration stays in YAML per
  the existing division of labor (Nuke owns build/publish logic; the workflow only wires triggers/caching/auth).
