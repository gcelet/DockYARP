## Why

`contributing.md` documents the change lifecycle and the build/test commands, but assumes a machine that
already has everything installed. It never mentions the OpenSpec CLI — central to every single change — or that
running it needs Node, which this repo does not otherwise depend on for anything but the docs site. A new
contributor has no single page telling them what to install before any of that works.

## What Changes

- Add an "Environment setup" section to `contributing.md`, placed before "Change lifecycle" (a contributor needs
  a working machine before the workflow matters): required tooling (.NET 10 SDK — or note `build.ps1`/`build.sh`
  fetch it automatically per `global.json`; Node, needed only for the OpenSpec CLI via `npx`; git), the OpenSpec
  CLI itself, and Docker called out as **only** required for `./build.ps1 E2E`.
- Add a clearly separated, optional subsection for **Claude Code** users specifically: the checked-in `.mcp.json`
  MCP servers and `.claude/commands/opsx`/`.claude/skills` wiring are conveniences layered on top of the OpenSpec
  CLI, not a second required toolchain.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `documentation`: extends the existing **Contributing and development guidance** requirement to also cover
  environment setup (required tooling, the OpenSpec CLI + its Node dependency, Docker scoped to E2E only, and
  the Claude-Code-specific tooling called out as optional).

## Impact

- Modified: `docs-site/content/en/docs/contributing.md`.
- No `src/`/`tests/` changes — documentation-only (AG-DOC).
