## 1. Environment setup section (AG-DOC)

- [x] 1.1 Add an "Environment setup" section to `contributing.md`, placed before "Change lifecycle".
- [x] 1.2 List required tooling for any contribution: .NET 10 SDK (note `build.ps1`/`build.sh` fetch it
      automatically per `global.json`), Node (needed for the OpenSpec CLI via `npx`), git.
- [x] 1.3 Document the OpenSpec CLI (`npx @fission-ai/openspec@latest`) explicitly as a required tool for every
      change, not assumed knowledge.
- [x] 1.4 Call out Docker as required only for `./build.ps1 E2E`, not the base contribution flow.
- [x] 1.5 Add a visually separated "Using Claude Code" subsection listing `.mcp.json`'s MCP servers and the
      `/opsx:*` slash commands as optional convenience on top of the OpenSpec CLI, not a second toolchain.

## 2. Validation (AG-DEP / AG-DOC)

- [x] 2.1 Built the docs site locally (`./build.ps1 Docs`) — succeeded, no Hugo errors.
- [x] 2.2 Run `npx @fission-ai/openspec@latest validate add-doc-contributor-setup --strict`.
