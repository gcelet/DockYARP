---
id: add-issue-templates
capability: deployment
agent: AG-DEP
tier: C-doc
priority: low
status: backlog
nginx-proxy: (internal — contribution workflow)
provenance: 2026-08-06 user idea — issue templates now that the GitHub repo exists; reconcile with the committed backlog
---

## Why
Now that `gcelet/DockYARP` exists, GitHub **issue templates** would give native triage (labels, milestones,
Projects, discussion threads) and an inbound entry point for bug reports / feature requests once the repo is
public. The tension to resolve: this must **not** create a second, competing backlog that drifts from the current
`openspec/backlog/` (which the user values — versioned, offline, drives `/opsx:propose`, works in the day→evening
push flow).

## Current state
- No `.github/ISSUE_TEMPLATE/`. The backlog lives in `openspec/backlog/items/*.md` — curated, spec-driving,
  offline, committed, and the source of truth for the work queue + parity (`openspec/backlog/parity.md`).

## Proposed change (sketch — coexistence, decide in design)
- Add `.github/ISSUE_TEMPLATE/` for **inbound** signals only: `bug_report`, `feature_request`, `question`, plus a
  `config.yml` (disable blank issues, add contact links). Keep them lightweight.
- **Keep `openspec/backlog/` as the single curated source of truth** for accepted/shaped work + parity.
- **Triage flow:** an accepted inbound issue is shaped into an `openspec/backlog/items/<id>.md` stub (reference the
  issue number in `provenance:`), then the issue is labeled/linked (`closes #N` on the eventual PR). The issue =
  discussion/tracking anchor; the stub = the spec-ready backlog form.
- **Decision — a dedicated "backlog item" issue template?** Lean **no**: it duplicates the openspec stub schema
  (two homes → drift). The openspec stub already *is* the backlog form. For a visual roadmap, prefer a **GitHub
  Project** board that mirrors the backlog over a backlog issue template.

## Acceptance criteria (→ scenarios)
- **WHEN** someone opens a new issue **THEN** they pick a structured template (bug / feature / question), not a blank.
- **WHEN** an inbound issue is accepted **THEN** it is shaped into an `openspec/backlog/` stub referencing the issue,
  so `openspec/backlog/` stays the single source of truth (no duplicated tracking).

## Notes / risks / references
- Highest value once the repo goes **public**; low priority while solo/private.
- Do NOT stand up two competing backlogs — `openspec/backlog/` stays SoT; issues are the inbound funnel into it.
- Refs: `openspec/backlog/README.md` (lifecycle), `AGENTS.md` (Change lifecycle). Sibling idea: GitHub Project board.
