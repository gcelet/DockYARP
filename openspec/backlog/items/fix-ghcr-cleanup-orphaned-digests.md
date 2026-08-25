---
id: fix-ghcr-cleanup-orphaned-digests
capability: deployment
agent: AG-DEP
tier: A-structural
priority: medium
status: backlog
nginx-proxy: (internal finding — CI/registry housekeeping, not a parity gap)
provenance: 2026-08-25 — user found real GHCR package listing showing dozens of untagged digests 3-6+ days
  old, still present despite the weekly cleanup workflow's own successful runs
---

## Why
`.github/workflows/ghcr-cleanup.yml`'s `tag-selection: tagged` only ever considers TAGGED versions for
deletion. Every already-deleted prerelease tag's multi-arch child digests (per-platform manifests, never
tagged themselves) become permanently orphaned the moment their tag is deleted — `tagged`-only selection
never revisits them again, so they accumulate forever regardless of age. Confirmed via a real GHCR package
listing screenshot: dozens of bare `sha256:...` untagged entries, 3-6+ days old, sitting alongside 2 tagged
prereleases that were simply too young for the last scheduled sweep (a separate, expected timing effect, not
a bug).

## nginx-proxy behavior
N/A — internal CI/registry housekeeping, not a proxy-behavior parity gap.

## DockYarp today
`snok/container-retention-policy@v3.1.0` is pinned — confirmed via the action's own README that this version
added a real safeguard: before deleting, it fetches the manifest of every KEPT tagged version and excludes
all of its child digests from the deletion candidate list. This makes `tag-selection: both` safe to use — a
still-referenced multi-arch child is protected, only genuinely orphaned untagged digests become candidates.

## Proposed change (sketch)
Already implemented: `tag-selection: tagged` → `both` in `ghcr-cleanup.yml`, temporarily with `dry-run: true`
to review the real candidate list from an actual `workflow_dispatch` run before trusting it — same discipline
this file's own history already used once for the original tagged-only sweep (3 real runs reviewed before
flipping to `dry-run: false`). A follow-up commit flips `dry-run` back to `false` once reviewed.

## Acceptance criteria (→ scenarios)
- **WHEN** a `workflow_dispatch` dry-run is triggered with `tag-selection: both` **THEN** the candidate list
  is reviewed and contains only genuinely orphaned untagged digests (no digest still referenced by a kept tag).
- **WHEN** `dry-run` is flipped back to `false` after review **THEN** a real run actually deletes the
  reviewed orphaned digests.

## Notes / risks / references
- Deletion is irreversible — the dry-run review step is not optional, matching this file's own established
  precedent.
- Refs: `add-ghcr-image-retention`'s archived change (original tagged-only sweep + its own dry-run precedent).
