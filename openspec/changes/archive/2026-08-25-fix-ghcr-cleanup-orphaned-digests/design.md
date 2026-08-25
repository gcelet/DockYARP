## Context

See `proposal.md`. `snok/container-retention-policy@v3.1.0`'s own documented safeguard: before deleting, it
fetches the OCI manifest of every version it is about to KEEP, and removes all of that manifest's child
digests from the deletion candidate list — a multi-arch image index's per-platform children stay protected
as long as their parent tag is kept.

## Goals / Non-Goals

**Goals:**
- Actually clean up orphaned untagged multi-arch child digests left behind by already-deleted tags.
- Verify via a real dry-run before trusting the wider candidate set.

**Non-Goals:**
- Not changing the `cut-off`/schedule cadence — that's the separate, expected "images accumulate between
  weekly runs" timing effect already explained to the user, not this item's concern.
- Not changing `image-tags`/`image-names` filters.

## Decisions

**`tag-selection: both`, relying on v3.1.0's kept-tag protection rather than hand-rolling orphan detection.**
The action already solves "don't delete a digest still referenced by something we're keeping" — no need to
duplicate that logic ourselves (e.g., cross-referencing manifests manually before deletion).

**Two-commit rollout (`dry-run: true` then a follow-up `false`)**: mirrors this file's own real precedent
(3 reviewed dry runs before the original tagged-only sweep went live) — deletion is irreversible, and
widening to `both` considers a materially larger candidate set than ever reviewed before.

## Risks / Trade-offs

- [Risk] The dry-run's candidate list could include a digest still referenced by a tag outside this run's
  visibility (e.g., a very old release tag not recently touched) → Mitigation: v3.1.0's protection walks every
  KEPT tagged version's manifest, not just recently-created ones — review the dry-run output explicitly before
  flipping to real deletion regardless.
