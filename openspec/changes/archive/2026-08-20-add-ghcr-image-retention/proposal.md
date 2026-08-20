## Why

Every `develop` push publishes the edge channel under two tags: `edge` (moving) and a GitVersion-resolved
prerelease unique to that commit (e.g. `0.1.0-alpha.223`). The moving `edge` tag is useful; the per-commit one
is not — once superseded, nothing references it again, and it accumulates indefinitely in the GHCR package
with no cleanup. The user wants only official releases and the current `edge` tag retained automatically.

## What Changes

- New scheduled GitHub Actions workflow (`.github/workflows/ghcr-cleanup.yml`), weekly `cron`, using
  `snok/container-retention-policy@v3.1.0` (verified: latest release, published 2026-05-29 — multi-arch-aware
  since this version, protecting a kept tag's per-platform manifests from deletion, matching DockYarp's actual
  multi-architecture publishing).
- Policy: `image-tags: "*-*"` — targets only tags containing a hyphen as deletion candidates. Every DockYarp
  release tag (`X`, `X.Y`, `X.Y.Z`, `latest`) and `edge` itself never contain a hyphen; every GitVersion-resolved
  edge-history prerelease tag always does (`0.1.0-alpha.223`). This is a positive, narrow filter, not an
  enumerated exclusion list — it can't accidentally sweep a future release-tag shape that happens to match
  something on an exclusion list, and it needs no maintenance as the tag scheme evolves elsewhere.
- Requires a repository secret carrying a token with package-delete rights (the default `GITHUB_TOKEN` does not
  have this) — a **user action**, not something this change can provision itself; flagged explicitly in
  `tasks.md`.
- Pure registry maintenance — no change to `build/Build.cs`, `ci.yml`, or `image.yml`'s publish paths.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `deployment`: adds a requirement for scheduled GHCR image retention, alongside the existing "Continuous image
  publishing" requirement it complements (that one defines what gets tagged; this one defines what eventually
  gets pruned).

## Impact

- `.github/workflows/ghcr-cleanup.yml` (new).
- A new repository secret (name TBD at design time) — provisioned by the user, not by this change.
- `docs-site/content/en/docs/*` — likely no change needed (this is repository/CI maintenance, not something an
  operator running DockYarp configures); confirm at design time rather than assumed.
