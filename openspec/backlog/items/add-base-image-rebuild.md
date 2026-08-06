---
id: add-base-image-rebuild
capability: deployment
agent: AG-DEP
tier: A-structural
priority: low
status: backlog
nginx-proxy: (internal — image freshness / supply chain)
provenance: 2026-08-06 CI/ops backlog expansion (user idea)
---

## Why
The app image is built `FROM mcr.microsoft.com/dotnet/aspnet:10.0`. Between DockYarp releases the base image
receives security patches, so a published DockYarp image goes stale (unpatched OS/runtime CVEs). The image should
be rebuilt and republished when its base image changes.

## Current state
- Root `Dockerfile` `FROM` is a floating tag (not digest-pinned); nothing rebuilds on a base-image update.

## Proposed change (sketch)
Primary (deterministic, reviewed):
- **Pin the `FROM` to a digest** and let **Renovate** (`add-renovate-bot`) open a digest-bump PR when the base
  image updates; merging it triggers `add-ci-image-publish` (a new image with the patched base).
Safety net (optional):
- A **scheduled workflow** (`.github/workflows/base-image-refresh.yml`, daily cron) that compares the current
  `FROM` digest against the registry and, if changed, rebuilds + republishes (or opens a PR).

## Acceptance criteria (→ scenarios)
- **WHEN** the .NET base image publishes a new digest **THEN** a PR (Renovate) updates the pinned `FROM`, and on
  merge the app image is rebuilt and republished.
- **WHEN** the scheduled check runs and the base digest changed **THEN** a rebuild/PR is produced.

## Notes / risks / references
- Depends on `add-ci-image-publish` + `add-renovate-bot`. Digest-pinning also improves build reproducibility.
- Decide primary-only (Renovate) vs. also the scheduled net (belt-and-suspenders) in the design.
