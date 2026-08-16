---
id: add-doc-contributor-setup
capability: documentation
agent: AG-DOC
tier: C-doc
priority: low
status: backlog
nginx-proxy: (internal initiative — contributor onboarding, no parity row)
provenance: 2026-08-16 user request
---

## Why
`contributing.md` documents the OpenSpec **change lifecycle** and the build/test/docs commands, but assumes a
machine that already has everything installed. A new contributor — human or AI-agent-assisted — has no single
page listing what to install before any of that works, and OpenSpec itself (central to every change) is easy
to miss: it's invoked via `npx @fission-ai/openspec@latest`, which silently needs Node, which this project
manages via **fnm** and is **not on `PATH` by default**.

## nginx-proxy behavior
N/A — internal initiative (contributor onboarding, not a proxy feature). No `parity.md` row.

## DockYarp today
- `contributing.md` covers the change lifecycle + `dotnet build`/`test`/`./build.ps1 Test` — assumes .NET is
  already installed and working.
- `global.json` pins .NET 10 with `rollForward latestMinor`; `build.ps1`/`build.sh` (Nuke) download the SDK
  if missing — so the *build* itself is fairly self-bootstrapping, but that isn't stated anywhere for a human
  reading the docs site.
- The OpenSpec CLI (`@fission-ai/openspec`) is required for **every** change (propose/apply/archive) and needs
  Node — not documented on the site at all today (only in AI-agent-facing files: `CLAUDE.md`, this project's
  Claude Code memory).
- Docker is only needed for E2E (`./build.ps1 E2E`) — not for the unit/integration gate.
- `.mcp.json` (git / microsoft-docs / docker / aspire MCP servers) and `.claude/skills`+`.claude/commands/opsx`
  are **Claude-Code-specific** conveniences layered on top of OpenSpec, not a requirement to use OpenSpec itself
  (its CLI works standalone, from any editor/agent, or by hand per its own docs).

## Proposed change (sketch)
An "Environment setup" section (`contributing.md`, or a new page if it grows too long for that file — decide at
propose-time) listing, for a **human contributor with no AI tooling**:
- .NET 10 SDK — or note that `build.ps1`/`build.sh` fetch it automatically per `global.json`.
- Node.js — needed only to run the OpenSpec CLI via `npx`; note this project manages Node via **fnm** but that
  is this maintainer's choice, not a hard requirement (any Node ≥ OpenSpec's minimum works).
- The OpenSpec CLI itself (`npx @fission-ai/openspec@latest`) — the tool every change goes through.
- Docker (Desktop or Engine) — **only** required for `./build.ps1 E2E`, called out as optional for most changes.
- git.
Then a clearly separated, **optional** subsection for contributors using **Claude Code** specifically: the
checked-in `.mcp.json` MCP servers (git, microsoft-docs, docker, aspire) auto-enable via
`.claude/settings.json`, and the `/opsx:*` slash commands wire the OpenSpec lifecycle — note these are
conveniences, not a second required toolchain.

## Acceptance criteria (→ scenarios)
- **WHEN** a new contributor with a clean machine reads the doc site **THEN** they can go from clone to a green
  `dotnet test` **and** a working `npx @fission-ai/openspec@latest list` using only that page — no need to
  discover the OpenSpec CLI or the Node dependency by trial and error.
- **WHEN** a contributor is not using Claude Code **THEN** the page makes clear which parts (MCP servers,
  slash commands) do not apply to them and what they'd do instead (the OpenSpec CLI directly).
- **WHEN** a contributor wants to run E2E **THEN** the Docker requirement is called out as specific to that,
  not the base contribution flow.

## Notes / risks / references
- Keep this scoped to **setup**, not a restatement of the change lifecycle (`contributing.md` already covers
  that) or the release process (see the sibling `add-doc-release-guide`).
- Refs: `global.json`, `build.ps1`/`build.sh`, `.mcp.json`, `.claude/settings.json`, `CLAUDE.md`,
  `docs-site/content/en/docs/contributing.md`.
