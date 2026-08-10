---
id: add-e2e-release-gate
capability: deployment
agent: AG-DEP
tier: B-runtime
priority: medium
status: backlog
nginx-proxy: (internal — release quality)
provenance: 2026-08-06 user request — E2E must gate at least release creation (not necessarily the per-push CI)
---

## Why
The end-to-end suite validates real runtime behavior (TLS/ACME/mTLS handshakes, Docker discovery, per-vhost policy)
that unit/integration tests cannot. It is deliberately **not** in the per-push CI gate (speed + Aspire/DCP timing
flakiness, needs Docker), but a broken release must not be publishable — so **E2E must gate at least release
creation**. Today `image.yml` publishes on a `v*` tag with **no E2E gate**.

## Current state
- Per-push CI (`ci.yml`) runs only `./build.sh Test` (unit + integration; E2E excluded).
- The Nuke **`Release`** target already `DependsOn(Test, E2E, DockerImage)` — the E2E gate **exists locally** but is
  not wired into the release CI.
- Release publish (`image.yml`, on `v*`) runs `DockerPublish` directly, skipping E2E.

## Proposed change (sketch)
- On a release (`v*` tag): run the **E2E suite on a Docker-capable runner** (GitHub `ubuntu-latest` has Docker)
  **before** publishing — e.g. an `e2e` job that `image.yml`'s publish job `needs:`, or run `./build.sh Release`
  (Test + E2E + DockerImage) as the gate. A failing E2E **blocks the release**.
- **Reliability decision (design):** DCP-on-CI vs the **non-DCP harness** (`add-nondcp-e2e-harness`) — the DCP timing
  flakiness may make the non-DCP harness a prerequisite for a dependable release gate. Decide in design.
- Optional: a **nightly scheduled** E2E run for early signal, independent of releases.

## Acceptance criteria (→ scenarios)
- **WHEN** a `vX.Y.Z` release is triggered **THEN** the E2E suite runs and must pass before the image is published.
- **WHEN** the E2E suite fails on a release **THEN** the image is NOT published and the release fails.

## Notes / risks / references
- **Repo-dependent** for real runs (author + `actionlint` / local `./build.sh Release` now; live run on the repo).
- E2E flakiness under Aspire/DCP on CI runners → see `add-nondcp-e2e-harness` (likely prerequisite for reliability).
- `fetch-depth: 0` for GitVersion. Ties into `add-ci-image-publish` (the publish it gates) and `add-release-changelog`
  (the release workflow it plugs into).
