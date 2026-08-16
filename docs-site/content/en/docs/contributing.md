---
title: Contributing
weight: 7
description: The spec-driven workflow behind DockYARP.
---

DockYARP is developed **spec-first** with [OpenSpec](https://github.com/Fission-AI/OpenSpec). Every change —
feature, fix, or refinement — follows the same loop.

## Environment setup

Required for any contribution:

- **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)** — or skip installing it: `build.ps1` /
  `build.sh` (Nuke) fetch the SDK pinned by `global.json` automatically if it's missing.
- **[Node.js](https://nodejs.org/)** — needed to run the **OpenSpec CLI**
  (`npx @fission-ai/openspec@latest`), the tool every change goes through (propose / apply / archive). This repo
  has no other Node dependency outside the docs site.
- **[git](https://git-scm.com/)**.
- **[Docker](https://www.docker.com/)** — only required for `./build.ps1 E2E`. The unit/integration gate
  (`./build.ps1 Test`) needs none.

### Using Claude Code

Optional, on top of the above — not a second toolchain. The checked-in `.mcp.json` (git / microsoft-docs /
docker / aspire MCP servers) auto-enables via `.claude/settings.json`, and `.claude/commands/opsx/*` +
`.claude/skills/openspec-*` wire the `/opsx:*` slash commands to the OpenSpec CLI above. None of this is
required to contribute — the OpenSpec CLI works standalone from any editor or agent.

## Change lifecycle

1. **Backlog** — an item under `openspec/backlog/items/<id>.md` describes the gap (the parity matrix
   `openspec/backlog/parity.md` tracks nginx-proxy ↔ DockYARP coverage).
2. **Propose** — author the change's `proposal.md` / `design.md` / `tasks.md` / spec delta.
3. **Apply** — implement it, with the build and tests green.
4. **Archive** — commit, then sync the spec library and archive the change.
5. **Close the loop** — remove the backlog item and flip its parity row to ✅.

## Build & test

```bash
dotnet build DockYarp.slnx
dotnet test DockYarp.slnx      # or ./build.ps1 Test (Nuke)
```

Quality is enforced at compile time (warnings are errors, strict analyzers, XML docs). See `AGENTS.md` for the
full conventions.

## Testing

DockYARP follows a **test pyramid** — each layer proves what the one below cannot, so the slow end-to-end suite
stays small:

- **Unit** (per `*.Tests` project) — pure logic; most coverage lives here.
- **Integration** (`DockYarp.IntegrationTests`, `Microsoft.AspNetCore.Mvc.Testing`) — the ASP.NET pipeline
  in-process, no Docker.
- **End-to-end** (Aspire AppHost + Docker) — only what needs the real running stack: discovery, live TLS/ALPN/ACME,
  protocol negotiation.

Add an e2e **only** for behavior unit and integration cannot prove. The full map of what each e2e covers — and what
is deliberately *not* — lives in [`docs/testing.md`]({{< repo-file "docs/testing.md" >}}); keep it in sync when you
add or remove an e2e.

Run the gates with Nuke:

```bash
./build.ps1 Test   # unit + integration (the CI gate)
./build.ps1 E2E    # end-to-end (requires Docker)
./build.ps1 Docs   # build this documentation site
```

## Architecture

For a map of the modules and how a request flows through the proxy, see
[`docs/architecture.md`]({{< repo-file "docs/architecture.md" >}}).

## Releases

See [Releasing]({{< relref "releasing.md" >}}) for the step-by-step process, including the one-time `main`
bootstrap at the first release.

## This documentation site

Lives under `docs-site/` (Hugo + Docsy). See its `README.md` for local setup and build.
