---
id: add-registry-readme-sync
capability: deployment
agent: AG-DEP
tier: A-structural
priority: low
nginx-proxy: n/a (DockYarp packaging/ops)
provenance: 2026-08-14 user backlog discussion (Docker Hub README once the image is published)
status: backlog
---

## Why
A container registry shows a repository **overview/README** next to the image. GHCR (the current default) can link the
repo README to the package automatically, but **Docker Hub does not** — its overview must be pushed via the Docker Hub
API, or it stays empty/stale. When DockYarp publishes its first image, the registry page should carry a concise,
accurate overview.

## Current state
- `image.yml` publishes to a **configurable registry** (GHCR default, or any registry via `vars.IMAGE_REGISTRY` +
  `secrets.REGISTRY_USERNAME/PASSWORD`); nothing syncs a registry description today. `README.md` has a docs badge and
  points to the live docs site.

## Proposed change (sketch)
- Author a **short registry overview** `docs/registry-README.md` (trimmed for a registry card: what DockYarp is, a
  minimal compose snippet, supported tags — link the full docs site; do **not** reuse the whole repo README).
- Add a workflow step (in `image.yml` or a small `registry-readme.yml`) that, **only when the target registry is Docker
  Hub**, pushes that file as the repository overview via `peter-evans/dockerhub-description` (or the Docker Hub API),
  gated on `secrets.DOCKERHUB_USERNAME`/`DOCKERHUB_TOKEN`. On GHCR this is a no-op (the linked repo README is used).
- Keep it in sync on each release (or on a `docs/registry-README.md` change).

## Acceptance criteria (→ scenarios)
- **WHEN** an image is published to Docker Hub and the Docker Hub secrets are set **THEN** the repository overview is
  updated from `docs/registry-README.md`.
- **WHEN** the target registry is not Docker Hub (e.g. GHCR) **THEN** the step is skipped (no failure).

## Notes / risks / references
- **Decision needed first**: is Docker Hub actually a target? `image.yml` defaults to GHCR. If DockYarp stays GHCR-only,
  this item is low value (GHCR shows the linked repo README) — do it only when Docker Hub publishing is chosen.
- Pairs with [`add-image-tag-strategy`](add-image-tag-strategy.md) (the "Supported tags" section of the overview should
  match the tag scheme). Refs: `.github/workflows/image.yml`, `README.md`.
