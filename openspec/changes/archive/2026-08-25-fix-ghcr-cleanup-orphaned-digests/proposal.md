## Why

`ghcr-cleanup.yml`'s `tag-selection: tagged` never revisits untagged multi-arch child digests once their
parent tag is deleted — they accumulate forever. Confirmed via a real GHCR package listing: dozens of
untagged digests 3-6+ days old, still present despite the weekly cleanup's own successful runs.

## What Changes

- `tag-selection: tagged` → `both` in `.github/workflows/ghcr-cleanup.yml`, safe because `v3.1.0` (already
  pinned) protects any digest still referenced by a kept tag.
- `dry-run: false` → `true` temporarily, to review a real candidate list before trusting the wider sweep —
  same discipline this file's own history already used once. A follow-up commit flips it back.

## Capabilities

Pure CI/registry housekeeping — no product-facing behavior changes. `skip_specs: true` is set in this
change's `.openspec.yaml`.

### New Capabilities
(none)

### Modified Capabilities
(none)

## Impact

- `.github/workflows/ghcr-cleanup.yml` only.
