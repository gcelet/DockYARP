@AGENTS.md

# Claude Code — DockYarp specifics

All project conventions live in **`AGENTS.md`** (imported above). This file only holds the bits
specific to Claude Code.

## Workflow

- **Spec-driven (OpenSpec)**: work lives in `openspec/changes/<id>/`. Before implementing, read the
  change's `proposal.md`, `specs/<capability>/spec.md`, and `design.md`, then follow its `tasks.md`
  checklist. Do not code beyond the scope of the current change. Use the `/opsx:*` slash commands
  (propose, apply, archive) and `npx @fission-ai/openspec@latest list|validate <id> --strict`.
- **Change lifecycle & backlog**: every change follows the loop in `AGENTS.md` ("Change lifecycle"); the
  entry point is the parity backlog `openspec/backlog/` (start at its `README.md`, pick an
  `items/<id>.md`). On archive, close the loop by setting the item `status: done` and flipping its row in
  `openspec/backlog/parity.md` to ✅.
- **Build/tests**: `dotnet build DockYarp.slnx`, `dotnet test DockYarp.slnx`. A warning breaks the
  build (`TreatWarningsAsErrors`) — fix it, don't disable the rule.
- **Packages**: versions are centralized in `Directory.Packages.props` (CPM), never a `Version=` in a
  `.csproj`.

## MCP

The project servers (`git`, `microsoft-docs`, `docker`) are defined in `.mcp.json` and auto-enabled via
`.claude/settings.json` (`enableAllProjectMcpServers`). Check their status with `/mcp`.

- `microsoft-docs`: reflex for validating a .NET/YARP API before using it.
- `git`: requires `uv`/`uvx` (installed). `docker`: requires Docker Desktop + MCP Toolkit.

Recommended alternative for docs: the Claude Code **`microsoft-docs` plugin** bundles the Microsoft Learn
MCP server plus skills (`/plugin` to install it). If you install it, it replaces the `microsoft-docs`
entry in `.mcp.json` — avoid running both at once.
