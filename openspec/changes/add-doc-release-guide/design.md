## Context

See `proposal.md` - Why. Relevant current state:
- `contributing.md` (weight 7) already has a short "Releases" section from `add-release-changelog`: 3 sentences,
  no worked example, no first-release bootstrap coverage.
- `GitVersion.yml` is GitFlow; `develop` is the trunk, `main` is created only at the first release.
- `.github/workflows/release.yml` (git-cliff + `softprops/action-gh-release`) and `.github/workflows/image.yml`
  (tagged image publish) are the two workflows a tag push triggers — this page should link to them (via the
  `{{< repo-file >}}` shortcode, which already centralizes the target branch through `params.github_branch` in
  `hugo.toml`), not restate their internals.
- Existing reference pages (`configuration.md` weight 2, `features.md` weight 3, `examples.md` weight 4) set the
  site's tone: real commands/keys, worked examples, no invented content.

## Goals / Non-Goals

**Goals:**
- One page a contributor can follow top-to-bottom to cut a release, including the one-time `main` bootstrap.
- Remove the duplicated 3-sentence version from `contributing.md`, replaced with a pointer.

**Non-Goals:**
- Changing the release process itself (no new tooling/automation) — this documents the process
  `add-release-changelog` already shipped, plus the manual `main`-bootstrap and tag steps that were never
  automated. The backlog item's own Notes flag automating tag-cutting as a *possible separate future item*.
- A worked example against a *real* tag (none has been cut yet — `main` doesn't exist). The worked example is
  necessarily illustrative (placeholder version numbers), not a screenshot of an actual release.

## Decisions

- **New page `releasing.md`, weight 8** (immediately after Contributing, weight 7) — release-cutting is a
  maintainer operation distinct from the contributor change-lifecycle content `contributing.md` covers; keeping
  it separate matches how `configuration.md`/`features.md`/`examples.md` are already split by concern rather than
  folded into one page.
- **Page structure**, in reading order:
  1. **First release only** — create `main` from `develop`, tag `v0.1.0`. Marked clearly as a one-time step so
     it doesn't confuse a contributor cutting the 2nd+ release.
  2. **Every release** — check the version GitVersion would compute (`dotnet gitversion` output, or read it off
     the last CI run), then `git tag vX.Y.Z && git push origin vX.Y.Z`.
  3. **What happens automatically** — a short bullet list (changelog + GitHub Release via `release.yml`, image
     publish via `image.yml`), each linking to its workflow file rather than re-explaining it.
  4. **Worked example** — one illustrative pass through steps 1-3 with placeholder values, clearly labeled as
     illustrative since no real release has shipped yet.
- **`contributing.md` edit**: replace the existing "Releases" section body with one or two sentences plus a link
  to `releasing.md`, keeping the section heading (so an existing anchor/mental-model of "releases live under
  Contributing" still resolves, just redirected).

## Risks / Trade-offs

- [Page describes a process with no real release yet run through it] → mitigated by labeling the worked example
  illustrative; the page still documents the *mechanics*, which are already real (the workflows exist and are
  tested — see `add-release-changelog`'s validation).
- [Duplication risk between this page and `release.yml`/`image.yml`] → mitigated by linking to the workflow
  files via `{{< repo-file >}}` for "what happens automatically," rather than re-describing their YAML.
