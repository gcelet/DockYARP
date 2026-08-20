---
id: add-ghcr-image-retention
capability: deployment
agent: AG-DEP
tier: B-runtime
priority: low
nginx-proxy: n/a (internal CI/registry hygiene)
status: backlog
provenance: 2026-08-20 user observation — the GHCR package for DockYarp accumulates a never-useful image per
  trunk push (GitVersion-resolved edge prerelease tag), never pruned; user wants an automatic purge keeping
  only official releases and the current edge tag
---

## Why

Every push to `develop` (no tag) publishes the edge channel with **two** tags: `edge` (moving) and a
GitVersion-resolved prerelease version unique to that commit (e.g. `0.1.0-alpha.223`, per the "Continuous
image publishing" requirement in `openspec/specs/deployment/spec.md`). The moving `edge` tag is genuinely
useful; the per-commit prerelease tag is not — once superseded by the next push, nothing references it again,
and it accumulates indefinitely in the GHCR package with no cleanup mechanism. The user wants only official
releases (`X.Y.Z`/`X.Y`/`X`/`latest`) and the current `edge` tag retained.

## Current state

- `.github/workflows/image.yml`'s `publish-edge` job pushes `{repository}:edge` and
  `{repository}:{GitVersion-resolved-prerelease}` on every `develop` push, per the existing tag-scheme
  requirement — no code change needed there, this item is purely about pruning what accumulates afterward.
- No cleanup workflow exists today. GitHub itself has no native retention-policy feature for container
  packages (confirmed via web search, 2026-08-20) — third-party GitHub Actions are the standard approach.
- DockYarp publishes **multi-architecture** manifests (`linux/amd64` + `linux/arm64`) — a cleanup tool that
  isn't multi-arch-aware could delete a kept tag's child platform manifests, breaking that "kept" image. This
  rules out naive/simple cleanup scripts.

## Proposed change (sketch)

- New scheduled GitHub Actions workflow (e.g. `.github/workflows/ghcr-cleanup.yml`), `cron`-triggered
  (weekly, exact cadence TBD at propose time), using `snok/container-retention-policy`
  (confirmed latest: `v3.1.0`, released 2026-05-29 — verify again before pinning, per this project's standing
  practice of checking third-party action versions rather than guessing) — chosen specifically because it
  protects multi-arch child manifests of kept tags since that version, matching DockYarp's actual publishing
  shape.
- Policy: keep tags matching stable release shapes (`X.Y.Z`, `X.Y`, `X`, `latest`) plus `edge`; delete
  everything else — sweeps the accumulating per-commit edge prerelease tags. Exact regex/include-exclude
  syntax to finalize at design time against the action's real configuration surface (don't guess the exact
  option names here).
- Pure registry-maintenance workflow, not a build/publish step — does not touch `build/Build.cs` or the
  existing `ci.yml`/`image.yml` publish paths; a separate, additive workflow file.

## Acceptance criteria (→ scenarios)

- **WHEN** the cleanup workflow runs **THEN** every stable release tag (`X.Y.Z`/`X.Y`/`X`/`latest`) and the
  current `edge` tag remain in the GHCR package, and multi-arch children of those kept tags are not deleted
  (the published image for a kept tag still pulls and runs correctly afterward).
- **WHEN** the cleanup workflow runs **THEN** superseded per-commit edge prerelease tags are removed from the
  package.

## Notes / risks / references

- Low priority, no urgency — this is registry hygiene, not a functional bug; safe to schedule whenever
  convenient.
- Real risk to validate at implementation time: confirm the cleanup action actually has permission to delete
  GHCR package versions (likely needs a PAT with `delete:packages` scope or the newer fine-grained equivalent,
  since the default `GITHUB_TOKEN` may not have package-deletion rights) — check this early, it may block a
  same-session same-repo validation.
