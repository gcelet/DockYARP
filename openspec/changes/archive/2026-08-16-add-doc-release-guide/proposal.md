## Why

`add-release-changelog` automated the *output* half of cutting a release (a `vX.Y.Z` tag push generates a
changelog and creates the GitHub Release), but nothing on the doc site walks a contributor through the *input*
half: when `main` gets created (a one-time bootstrap under GitFlow), how to read the version GitVersion would
compute, and the exact command to tag. That knowledge is currently scattered across `GitVersion.yml`, a 3-sentence
note in `contributing.md`, and this backlog's history. The user wants cutting a release to be as simple as
following one page, start to finish.

## What Changes

- Add a new doc-site page, **Releasing** (`docs-site/content/en/docs/releasing.md`, weight 8, right after
  Contributing), covering: the one-time `main`-creation bootstrap for the first release; reading the
  GitVersion-computed version before tagging; the exact tag command; and what happens automatically after
  (linking to, not duplicating, `release.yml`'s and `image.yml`'s behavior). Includes one worked example.
- Trim `contributing.md`'s existing "Releases" section (added by `add-release-changelog`) to a short pointer to
  the new page, so the release process is documented in exactly one place.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `documentation`: adds a **Release process reference** requirement (a standalone doc-site page walking a
  contributor through cutting a release, including the first-release `main` bootstrap), alongside the existing
  reference-page requirements (configuration, application configuration, runtime features, examples).

## Impact

- New: `docs-site/content/en/docs/releasing.md`.
- Modified: `docs-site/content/en/docs/contributing.md` (trim the Releases section to a pointer).
- No `src/`/`tests/` changes — documentation-only (AG-DOC).
