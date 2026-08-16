## Why

Cutting a DockYarp version today still ends at a `v*` git tag: `image.yml` publishes the tagged image, but there
is no GitHub Release and no changelog. Once a release is cut, consumers (and the "Supported tags" list a future
registry README would carry) have nothing to point at describing what shipped. The release-workflow decision
(reconciled 2026-08-16 against the GitFlow branching model) was to wire this on `main`, tool TBD between
release-please and git-cliff.

**Scope note (reconciled 2026-08-16):** under GitFlow, `main` does not exist yet (it is created at the *first*
release by merging `develop` in and tagging `v0.1.0`). This change does not create `main` or cut `v0.1.0`.

**Tool pivot (2026-08-16, during apply):** the original sketch picked `release-please`, but it turned out
**unable to parse this repo's gitmoji-prefixed commits** (`:sparkles: feat: …`) — confirmed against
[googleapis/release-please#2385](https://github.com/googleapis/release-please/issues/2385), an open, unresolved
upstream issue with no config-level workaround (its conventional-commit parser anchors on `type:` at the start
of the subject; a leading gitmoji token breaks the match, so every commit would be treated as non-conventional).
Switched to **`git-cliff`**, whose `commit_parsers` are free-form regexes over the whole subject and handle the
gitmoji prefix natively — this is the tool the stub's own "Release workflow" section had already flagged as the
regex-friendly alternative.

## What Changes

- Add `.github/workflows/release.yml`: on `push tags: v*` (the same trigger `image.yml` already reacts to), run
  `git-cliff` to generate the changelog for that tag (since the previous tag) and create/update the GitHub
  Release for it (via `softprops/action-gh-release`) with that changelog as the release body.
- Add `cliff.toml` configured to parse this repo's **gitmoji-prefixed Conventional Commits**
  (`:sparkles: feat: …`, `:bug: fix: …`) via regex `commit_parsers`, grouped into changelog sections (Features /
  Fixes / …), with `chore`-type commits — including `chore: archive <id> into specs` — excluded (`skip = true`)
  from the generated notes rather than merely hidden.
- **Version source stays GitVersion** (`add-release-versioning`) for build-time stamping (assemblies, image,
  `/api/version`) on every push. **Deciding/pushing the `vX.Y.Z` tag itself stays a manual, GitVersion-informed
  step** — same as today (`image.yml` already just reacts to whatever tag gets pushed); this change does not add
  automatic version-bump/tag-cutting, only changelog generation once a tag exists.
- No change to `image.yml`'s existing `push tags: v*` trigger.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `deployment`: adds a **Release changelog** requirement (GitHub Release + generated changelog created when a
  `vX.Y.Z` tag is pushed), alongside the existing Build versioning / Continuous image publishing requirements.

## Impact

- New: `.github/workflows/release.yml`, `cliff.toml`.
- No `src/`/`tests/` changes — this is a repo/CI-only change (AG-DEP), no code or spec-behavior change to the
  running proxy.
- Docs: note the release process in `docs/` if a contributing/release section exists (checked at apply time).
