---
id: add-doc-contribution-workflow
capability: documentation
agent: AG-DOC
tier: C-doc
priority: medium
status: backlog
nginx-proxy: (internal initiative — external contributor workflow, no parity row)
provenance: 2026-08-16 user request, following review of add-doc-contributor-setup
---

## Why
`contributing.md`'s "Change lifecycle" section describes the backlog → propose → apply → archive loop as if the
reader can push directly to `develop` — because today, the only contributor is the repo owner, working solo.
But **no external contributor ever has direct push access, regardless of the 1.0 milestone** (that milestone is
about when the *owner* switches from solo direct-push to their own feature branches — it says nothing about
outside contributors). The page currently has no answer to "I don't have push access, how do I actually submit
this?" — fork, branch, PR mechanics are entirely absent.

## nginx-proxy behavior
N/A — internal initiative (DockYarp's own contribution process, not a proxy feature). No `parity.md` row.

## DockYarp today
- `contributing.md` (after `add-doc-contributor-setup`) covers: Environment setup, Change lifecycle
  (backlog/propose/apply/archive/close-loop), Build & test, Testing, Architecture, a Releases pointer, and the
  doc-site meta note. No mention of forking, branches, or opening a pull request.
- `AGENTS.md` (edited 2026-08-16, same review) now documents the commit convention (gitmoji + Conventional
  Commits, the two-commit implementation/archive pattern) and carries **one line** noting that anyone without
  push access runs the same loop on a fork/branch and opens a PR — but that's the terse, AI-agent-facing source
  of truth, not a human-friendly walkthrough.
- `github-repo-init-plan` (project memory, not repo-visible) already settled the branching model: `develop` is
  the long-lived trunk pre-1.0 (owner commits directly, solo); `main` is created only at the first release and
  reserved for releases. This is real, decided information that has never been written anywhere a contributor
  would see it.

## Proposed change (sketch)
Add a section to `contributing.md` (e.g. "Submitting a contribution", placed after "Change lifecycle") covering:
- Fork the repository, branch off `develop`.
- Run the same OpenSpec loop locally (propose → apply, and archive too if the change carries a spec delta —
  point to the "Change lifecycle" section already on the page rather than repeating it).
- Follow the commit convention documented in `AGENTS.md` (link, don't duplicate it here).
- Open a pull request against `develop`, bundling those commits.
- State the current branch reality plainly: `develop` is the trunk pre-1.0; `main` is reserved for releases
  (created at the first one).

## Acceptance criteria (→ scenarios)
- **WHEN** a contributor without push access wants to submit a change **THEN** `contributing.md` tells them
  exactly what to do — fork, branch, run the loop, open a PR against `develop`.
- **WHEN** they need the exact commit format **THEN** the page links to `AGENTS.md` rather than restating it.
- **WHEN** they wonder which branch is the trunk **THEN** the page states `develop` is it pre-1.0, and that
  `main` is reserved for releases.

## Notes / risks / references
- Sibling, different concern: `add-issue-templates` covers the **inbound** side (how an idea becomes a backlog
  item via GitHub issues); this item covers the **outbound** side (how an accepted backlog item becomes a merged
  PR). Cross-link the two once both exist.
- Refs: `AGENTS.md` (Change lifecycle, now with the commit convention), `docs-site/content/en/docs/contributing.md`.
