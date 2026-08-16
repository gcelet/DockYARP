## Why

Now that `gcelet/DockYARP` exists, a new issue has no structure — no template steers a bug report, feature
request, or question, and nothing stops a blank issue. This is the **inbound** counterpart to
`add-doc-contribution-workflow` (which documented the **outbound** side — how an accepted change becomes a
merged PR): triage needs an entry point that funnels into the existing `openspec/backlog/` without creating a
second, competing backlog.

## What Changes

- Add `.github/ISSUE_TEMPLATE/` with three structured templates — `bug_report`, `feature_request`, `question` —
  plus a `config.yml` that disables blank issues.
- **No dedicated "backlog item" issue template.** `openspec/backlog/items/<id>.md` already *is* the spec-ready
  backlog form; a second issue-shaped version of the same schema would drift. An accepted inbound issue is
  instead shaped into a backlog stub that references the issue number in its `provenance:` field — the issue
  stays the discussion/tracking anchor, the stub stays the spec-driving source of truth.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `deployment`: adds a **Structured issue intake** requirement (templates for inbound bug/feature/question
  reports, with the triage path into `openspec/backlog/` stated), alongside the existing CI/CD requirements this
  capability already covers (continuous integration, dependency updates, image publishing).

## Impact

- New: `.github/ISSUE_TEMPLATE/bug_report.yml`, `.github/ISSUE_TEMPLATE/feature_request.yml`,
  `.github/ISSUE_TEMPLATE/question.yml`, `.github/ISSUE_TEMPLATE/config.yml`.
- No `src/`/`tests/` changes — repo-config-only (AG-DEP).
