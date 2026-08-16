## Context

See `proposal.md` - Why. Relevant current state, checked before writing:
- `global.json` pins .NET `10.0.100` with `rollForward: latestMinor` — any installed .NET 10 works, and
  `build.ps1`/`build.sh` (Nuke) fetch the SDK automatically if missing.
- `.config/dotnet-tools.json` restores `gitversion.tool` automatically via the Nuke build (`RestoreTools`) — not
  something a contributor installs by hand.
- The OpenSpec CLI is `@fission-ai/openspec`, run via `npx @fission-ai/openspec@latest` — needs Node. The docs
  build's CI (`.github/workflows/docs.yml`) pins **Node 24** for `hugo-extended`/Docsy, but the OpenSpec CLI
  itself has no such floor; the two Node needs are independent (a contributor who never touches the docs site
  still needs *some* Node for OpenSpec).
- `.mcp.json` (git / microsoft-docs / docker / aspire MCP servers) auto-enables via `.claude/settings.json`
  (`enableAllProjectMcpServers: true`); `.claude/commands/opsx/*` + `.claude/skills/openspec-*` wire the
  `/opsx:*` slash commands. All of this is Claude-Code-specific — the OpenSpec CLI works standalone.
- Docker is only invoked by `./build.ps1 E2E` — the unit/integration gate (`./build.ps1 Test`) needs none.

## Goals / Non-Goals

**Goals:**
- One place in `contributing.md` a new contributor reads *first*, before the change-lifecycle content, listing
  exactly what to install.
- Draw a clean line between "required for any contribution" and "Claude-Code-specific convenience."

**Non-Goals:**
- A full OS-by-OS installation walkthrough (links to each tool's own install docs are enough — this is a
  checklist, not a tutorial).
- Re-documenting the change lifecycle or the release process (siblings `contributing.md`'s existing "Change
  lifecycle" section and `add-doc-release-guide`'s Releasing page already cover those).

## Decisions

- **Extend `contributing.md`, no new page.** Unlike `add-doc-release-guide` (release-cutting is a distinct,
  meaty maintainer workflow that earned its own page), environment setup is a short checklist a new contributor
  reads once, immediately before the content already on this page — splitting it out would just add a click.
- **Placement: before "Change lifecycle."** A contributor needs a working machine before the workflow is
  actionable; setup reads naturally as the page's first section, ahead of what currently opens it.
- **Section structure**: a short "required for any contribution" list (.NET SDK note, Node + the OpenSpec CLI,
  git, Docker-only-for-E2E caveat), followed by a visually separated "Using Claude Code" subsection listing the
  MCP servers and slash commands as optional convenience, not a second required toolchain.
- **Don't pin a Node version for the OpenSpec CLI.** The docs-site's Node 24 requirement (Docsy/hugo-extended) is
  unrelated to what the OpenSpec CLI needs; stating "Node 24" here would overstate the requirement for a
  contributor who never touches `docs-site/`. State the docs-site-specific version only where it's already
  relevant (docs-site's own README), not duplicated here.

## Risks / Trade-offs

- [Section could drift from the actual toolchain over time] → kept deliberately short and link-heavy (to
  `global.json`, `.mcp.json`, the OpenSpec CLI's own docs) rather than restating version numbers that change.
