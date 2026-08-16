## Why

`contributing.md`'s "Change lifecycle" section describes the backlog → propose → apply → archive loop as if the
reader can push directly to `develop` — true today only because the sole contributor is the repo owner, working
solo. But no external contributor ever has direct push access, regardless of the 1.0 milestone (that milestone
is about when the *owner* moves from solo direct-push to their own feature branches — it says nothing about
outside contributors). The page has no answer today to "I don't have push access, how do I actually submit
this?": no mention of forking, branches, or opening a pull request.

## What Changes

- Add a "Submitting a contribution" section to `contributing.md`, placed right after "Change lifecycle" (the
  loop is the *what*; this section is the *how* for anyone without push access): fork the repository, branch off
  `develop`, run the same loop locally (propose → apply, and archive too if the change carries a spec delta —
  pointing back to "Change lifecycle" rather than repeating it), follow the commit convention now documented in
  `AGENTS.md` (link, don't duplicate), open a pull request against `develop`.
- State plainly which branch is the trunk: `develop` pre-1.0; `main` is reserved for releases, created at the
  first one (already documented for the release process itself on the Releasing page — this section only needs
  the one-line branch-model fact, not a repeat of the release mechanics).

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `documentation`: extends the existing **Contributing and development guidance** requirement to also cover how
  a contributor without push access submits a change (fork, branch, the same loop, a pull request against
  `develop`), and to state the current branch model.

## Impact

- Modified: `docs-site/content/en/docs/contributing.md`.
- No `src/`/`tests/` changes — documentation-only (AG-DOC).
