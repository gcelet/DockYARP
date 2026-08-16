## Context

See `proposal.md` - Why. Relevant current state:
- No `.github/ISSUE_TEMPLATE/` exists yet — any new issue today is blank.
- `openspec/backlog/README.md` already states the item front-matter schema (`id`, `capability`, `agent`, `tier`,
  `priority`, `status`) and the lifecycle (`backlog item → /opsx:propose <id> → ... → archive`) — this change
  doesn't touch that schema, only adds an inbound funnel into it.
- Sibling, already shipped this session: `add-doc-contribution-workflow` documents the **outbound** half (fork,
  branch, PR) on the doc site. This item is the **inbound** half (GitHub-native, not doc-site content).

## Goals / Non-Goals

**Goals:**
- Give an inbound reporter a structured template instead of a blank issue.
- Make the triage path (issue → backlog stub) explicit so `openspec/backlog/` never gets a second, drifting
  competitor inside GitHub Issues.

**Non-Goals:**
- A "backlog item" issue template mirroring the stub schema — rejected in the backlog stub itself: it would
  duplicate `openspec/backlog/items/<id>.md`'s schema in a second home, and the two would drift. The openspec
  stub already *is* the backlog form.
- A GitHub Project board / visual roadmap — noted as a sibling idea in the stub, not this item's scope.
- A pull request template — out of scope; the stub and this proposal are about **issue** intake specifically.

## Decisions

- **GitHub Issue Forms (YAML), not legacy Markdown templates.** Issue Forms give structured fields (dropdowns,
  required textareas) instead of a Markdown skeleton a reporter can ignore or delete — a stronger nudge toward
  usable reports, and the current GitHub-recommended format.
- **Three templates**: `bug_report.yml`, `feature_request.yml`, `question.yml` — matching exactly the acceptance
  criteria in the backlog stub. Each stays lightweight (a handful of fields), not a bureaucratic form.
  - `bug_report.yml`: what happened / expected behavior / DockYarp version / relevant config (labels, env vars)
    / logs.
  - `feature_request.yml`: problem/motivation / proposed behavior / whether it's nginx-proxy parity or a
    DockYarp-specific addition (mirrors how `openspec/backlog/parity.md` already classifies gaps).
  - `question.yml`: free-form question field; a note pointing at `docs-site` (the live site) and
    `openspec/backlog/parity.md` as likely-already-answered places to check first.
- **`config.yml`: `blank_issues_enabled: false`, no `contact_links`.** No other support channel (chat, forum)
  currently exists to link to; adding a placeholder `contact_links` entry would be inventing a channel that
  isn't there. Revisit if one is stood up later.
- **Labels**: each template sets a matching label (`bug`, `enhancement`, `question`) on creation, using GitHub's
  built-in default label set rather than inventing a new label taxonomy.

## Risks / Trade-offs

- [Issue Forms need GitHub's UI to render — no local/offline preview] → mitigated by keeping the YAML small and
  following GitHub's documented schema exactly; validated by review, not a build step (no CI check exists for
  issue-template schema and none is being added here — low value for 3 small forms).
