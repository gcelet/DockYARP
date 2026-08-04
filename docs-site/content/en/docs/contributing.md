---
title: Contributing
weight: 7
description: The spec-driven workflow behind DockYARP.
---

DockYARP is developed **spec-first** with [OpenSpec](https://github.com/Fission-AI/OpenSpec). Every change —
feature, fix, or refinement — follows the same loop.

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

## This documentation site

Lives under `docs-site/` (Hugo + Docsy). See its `README.md` for local setup and build.
