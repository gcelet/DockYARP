## Context

See `proposal.md` - Why. Relevant current state, checked before writing:
- `contributing.md` now has (after `add-doc-contributor-setup`): Environment setup, Change lifecycle, Build &
  test, Testing, Architecture, a Releases pointer, and the doc-site meta note. Nothing about forking or PRs.
- `AGENTS.md` (edited in the same review session that surfaced this gap) now documents the commit convention
  (gitmoji + Conventional Commits, the two-commit implementation/archive pattern) and carries one terse line
  noting anyone without push access runs the same loop on a fork/branch and opens a PR. This section is the
  human-friendly, doc-site version of that same fact — it should link to `AGENTS.md` for the commit format, not
  restate it (the existing requirement already forbids duplicating authoritative in-repo docs).
- Branch model (project memory, not repo-visible before this change): `develop` is the long-lived trunk pre-1.0;
  `main` is created only at the first release and reserved for releases. This has never been stated anywhere a
  contributor would see it.

## Goals / Non-Goals

**Goals:**
- Answer, on the page itself, "I don't have push access — how do I actually submit this?"
- State the branch model in one place a contributor would actually look.

**Non-Goals:**
- Repeating the change-lifecycle steps (backlog/propose/apply/archive) — this section points back to that
  section rather than duplicating it.
- Repeating the commit format — link to `AGENTS.md`, which now carries it (see Context).
- GitHub-specific PR mechanics (draft PRs, review requirements, CODEOWNERS) — that's `add-issue-templates`
  territory (GitHub-side scaffolding) if/when needed, not prose on this page.

## Decisions

- **Placement: a new "Submitting a contribution" section right after "Change lifecycle."** The lifecycle
  section is the *what* (the loop every change follows); this section is the *how* for someone who can't push
  directly — reading naturally as the next question after the loop is understood.
- **Link to `AGENTS.md` for the commit format**, not restate it — matches the existing (and now-modified)
  requirement's own rule about pointing to authoritative in-repo docs rather than duplicating them, same
  pattern already used for `docs/testing.md` and `docs/architecture.md` via `{{< repo-file >}}`.
- **State the branch model as a plain fact, one sentence**: `develop` is the trunk pre-1.0, `main` is reserved
  for releases. No need to explain *why* (the reasoning — anonymized commit timing, etc. — is the owner's own
  workflow detail, not something that belongs in a contributor-facing doc, and isn't relevant to how a
  contributor's own PR gets merged).

## Risks / Trade-offs

- [Branch model changes post-1.0 when the owner also starts using feature branches] → the fact being stated
  here ("which branch is the trunk today") stays accurate regardless; if the model changes materially, that's a
  future doc update, not a design risk of this change.
