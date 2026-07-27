# AGENTS.md — DockYarp

Guide for any AI agent (Claude Code, Copilot, Cursor, Codex…) working on this repository.
This file is the **source of truth** for conventions. Tool-specific files point back to it
(`CLAUDE.md`, `.github/copilot-instructions.md`).

> Principle: this document describes **intent**. The **exact rules** (severities, styles, checks) are
> enforced by configuration — `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`.
> When in doubt, **configuration wins**: don't restate the rules here, fix the code.

## The project

DockYarp is a **dynamic reverse proxy** for Docker containers (an `nginx-proxy` equivalent),
built on **YARP** and **.NET 10 / C#**. Target features: Docker auto-discovery, automatic ACME/TLS,
dynamic routes and clusters, security middleware, Admin API.

Architecture and work are **spec-driven with OpenSpec** (Fission-AI). The `openspec/` folder is the
source of truth:
- `openspec/config.yaml` — project context (stack, conventions, roadmap, cross-cutting backlog) injected
  into artifact generation.
- `openspec/changes/<id>/` — change proposals (`proposal.md`, `specs/<capability>/spec.md`, `design.md`,
  `tasks.md`). One change per roadmap phase.
- `openspec/specs/<capability>/spec.md` — the capability spec library (populated as changes are archived).

Domain agents referenced in tasks: `AG-RP` (proxy/YARP), `AG-DD` (Docker discovery/labels),
`AG-AT` (ACME/TLS/Kestrel), `AG-SEC` (security/auth), `AG-AA` (Admin API/observability),
`AG-DEP` (packaging/image/compose/CI).

**Before implementing: read the change's `proposal.md`, `specs/`, `design.md`, and work the `tasks.md`
checklist.** Use `npx @fission-ai/openspec@latest list` to see changes and the `/opsx:*` slash commands
(propose, apply, archive) to drive the workflow.

## Change lifecycle

Every change to DockYarp — feature, fix, or refinement — follows the **same loop**, so nothing is lost
between work sessions. The entry point is the parity backlog `openspec/backlog/` (see its `README.md`);
`openspec/backlog/parity.md` is the source-of-truth nginx-proxy ↔ DockYarp parity matrix that
`docs/architecture.md` links to.

1. **Backlog** — ensure the work has an item `openspec/backlog/items/<id>.md` (add one if it is new). The
   item's `id` is the future change id; its *Why* + *Acceptance criteria* seed the proposal.
2. **Propose** — `/opsx:propose <id>` → author `proposal.md` / `design.md` / `tasks.md` /
   `specs/<capability>/spec.md`.
3. **Apply** — `/opsx:apply` → implement; Nuke `Test` gate green.
4. **Commit + archive** — commit, then `/opsx:archive <id>` (syncs `openspec/specs/`).
5. **Close the loop** — **remove** the item file `openspec/backlog/items/<id>.md` from the backlog and flip
   its `parity.md` row to ✅ (the parity matrix keeps the permanent record; update `docs/` if user-facing).

## Layout

```
src/
  DockYarp.Core/      # models, interfaces, stores (leaf — depends on nothing)
  DockYarp.Docker/    # Docker discovery + label mapping           -> Core
  DockYarp.Tls/       # ACME + certificates                        -> Core
  DockYarp.Security/  # HTTPS enforcement, auth                    -> Core
  DockYarp.AdminApi/  # admin/observability endpoints              -> Core
  DockYarp.App/       # ASP.NET host (Web SDK): YARP, DI, pipeline -> everything
tests/                   # one *.Tests project per src project (NUnit)
build/                   # Nuke build project — DO NOT touch the Directory.Build.* stop-files
docs/                    # architecture documentation
specs/                   # specs driving the implementation
```

Dependency graph: `Core` is the leaf; every module references `Core`; `App` references everything.
Do not introduce cycles and do not make `Core` depend on a module.

## Commands

| Action | Command |
|---|---|
| Restore | `dotnet restore DockYarp.slnx` |
| Build | `dotnet build DockYarp.slnx` (or `./build.ps1` / `./build.sh` — Nuke) |
| Tests | `dotnet test DockYarp.slnx` |
| Run the app | `dotnet run --project src/DockYarp.App` |
| Format | `dotnet format DockYarp.slnx` |

The SDK is pinned by `global.json` (.NET 10). `build.ps1`/`build.sh` download the SDK if missing.

## .NET 10 / modern C# conventions

Quality is **enforced at compile time**: `TreatWarningsAsErrors=true`, `Nullable=enable`,
XML docs generated, and 6 strict analyzers (AsyncFixer, CSharpGuidelinesAnalyzer, Meziantou, Roslynator,
SonarAnalyzer, StyleCop). A warning is a red build.

- **Do not bypass an analyzer** with `#pragma warning disable` or `[SuppressMessage]` without a written
  justification (a comment explaining why). Fix the root cause first.
- **Modern syntax**: file-scoped `namespace`, `using` inside the namespace, primary constructors,
  collection expressions `[...]`, pattern matching, `required`/`init`, `record` / `readonly struct`
  for DTOs and value types, `is null` (not `== null`).
- **Async/await end-to-end** on I/O (Docker events, HTTP, ACME):
  - never `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`, or `async void` (except event handlers);
  - always flow a `CancellationToken`;
  - `ValueTask` on hot/often-synchronous paths; `ConfigureAwait(false)` in library code
    (`Core`, `Docker`, `Tls`, `Security`), not needed in the ASP.NET host.
- **Low allocation** (the proxy is a hot path):
  - `Span<T>` / `ReadOnlySpan<char>` / `Memory<T>`, `ArrayPool<T>`, `IBufferWriter<T>`;
  - explicit `StringComparison` (never culture-implicit comparisons);
  - avoid LINQ, allocating closures and boxing on the hot path; prefer `static` lambdas;
  - `sealed` by default on classes not designed for inheritance.
- **Bounded complexity** (already enforced by .editorconfig): ~40 statements max per method,
  ≤ 5 parameters (≤ 8 for a constructor). If you get stuck, refactor.
- **Immutability**: prefer immutable types and `readonly` fields.

## Comments & XML documentation

Everything committed is written in **English** — code, comments, and docs.

- **Code comments**: concise and they must **justify** — explain *why* (intent, trade-off, non-obvious
  constraint, workaround). A comment that only paraphrases the code is noise; delete it and let the code
  speak. Prefer clear names over explanatory comments.
- **XML documentation**: document public API with the **standard XML tags**
  (`<summary>`, `<param>`, `<returns>`, `<remarks>`, `<exception>`, `<typeparam>`, `<see>`, `<paramref>`…).
  - `<summary>` must be **short** — one crisp sentence stating what the member is/does.
  - Longer explanations, background, examples, or usage notes go in **`<remarks>`**, not in `<summary>`.
  - `GenerateDocumentationFile` is on; keep docs accurate — a wrong doc is worse than none.

## Dependencies (Central Package Management)

CPM is **enabled**. Versions live **only** in `Directory.Packages.props`.
- Add a package: add `<PackageVersion Include="X" Version="Y" />` to `Directory.Packages.props`,
  then `<PackageReference Include="X" />` **without `Version`** in the `.csproj`.
- **Never** put a `Version=` attribute in a `.csproj`.

## Tests

- NUnit 4 + AwesomeAssertions; coverage via coverlet.
- One `*.Tests` project per source project; `IsPackable=false`.
- Use `AwesomeAssertions` (`result.Should()…`) for assertions.
- HTTP integration tests: `Microsoft.AspNetCore.Mvc.Testing` (`DockYarp.IntegrationTests`).

## Guardrails

- Do not edit `.editorconfig` / `Directory.*.props` to make a build pass: fix the code instead.
- Do not touch the stop-files `build/Directory.Build.props` / `build/Directory.Build.targets`.
- Respect the `DockYarp.slnx` solution structure and CPM.

## Available MCP servers

Configured in `.mcp.json` (see `.claude/settings.json` for Claude Code activation):

| Server | Use | Prerequisite |
|---|---|---|
| `git` | status, diff, log, history, branches | `uv`/`uvx` (Python) — installed |
| `microsoft-docs` | check an up-to-date .NET/C#/ASP.NET/YARP API before coding | none (HTTP) |
| `docker` | inspect containers/labels while developing discovery | Docker Desktop + MCP Toolkit |

> Habit: consult **microsoft-docs** before using a .NET/YARP API you are unsure about,
> instead of guessing a signature.
