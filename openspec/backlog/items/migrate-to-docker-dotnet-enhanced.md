---
id: migrate-to-docker-dotnet-enhanced
capability: docker-discovery
agent: AG-DD
tier: A-structural
priority: low
nginx-proxy: n/a (internal finding — AOT/trim readiness, from investigate-aot-build)
provenance: 2026-08-23 investigate-aot-build spike, sharpened by user-provided leads (dotnet/Docker.DotNet#706, testcontainers/Docker.DotNet)
status: backlog
---

## Why

The `investigate-aot-build` spike first concluded `Docker.DotNet` was the one Native AOT blocker outside
DockYarp's control — no released version drops `Newtonsoft.Json`, and its own unreleased branch has no
public way to plug in AOT-safe JSON handling
([dotnet/Docker.DotNet#689](https://github.com/dotnet/Docker.DotNet/issues/689), open and unanswered since
2024-11-04). A follow-up look prompted by [PR #706 "Support AOT"](https://github.com/dotnet/Docker.DotNet/pull/706)
found the real path forward: the upstream repo is confirmed inactive by its own community, and
**[`testcontainers/Docker.DotNet`](https://github.com/testcontainers/Docker.DotNet)** is an actively
maintained fork, published on NuGet as `Docker.DotNet.Enhanced`, that already declares
`<IsAotCompatible>true</IsAotCompatible>`. This turns the last "wait for someone else" blocker on the path
to Native AOT into ordinary migration work DockYarp can do on its own schedule.

## Assessment (2026-08-23)

`Docker.DotNet.Enhanced` (package id, NuGet):
- Actively maintained: releases from `3.125.15` (the same version DockYarp currently pins, as the fork
  point) through `4.3.3` (published 2026-06-28); commits as recent as 2026-08-17.
- Targets `net8.0`/`net9.0`/`net10.0` plus `netstandard2.0`/`2.1`; `Directory.Build.props` sets
  `<IsAotCompatible>true</IsAotCompatible>` explicitly.
- Fully migrated to `System.Text.Json` — no `Newtonsoft.Json` dependency anywhere in the fork.
- Restructured into sub-packages (`Docker.DotNet.Handler.Abstractions`, `.LegacyHttp`, `.NativeHttp`,
  `.NPipe`, `.Unix`) and a different client-construction API: `new DockerClientBuilder().Build()` /
  `.WithEndpoint(...)` / `.WithContext(...)`, replacing the current `DockerClientConfiguration` /
  `CreateClient()` pattern.
- Namespace stays `Docker.DotNet`/`Docker.DotNet.Models` (confirmed via the fork's own `Using` includes),
  but the package id changes (`Docker.DotNet` → `Docker.DotNet.Enhanced`) and the major version jump
  (3.x → 4.x) signals real breaking changes beyond just the client construction API.
- DockYarp's entire Docker API surface goes through one file:
  `src/DockYarp.Docker/Discovery/DockerContainerSource.cs` — the migration's blast radius is contained.

## Proposed change (sketch)

1. Replace the `Docker.DotNet` package reference with `Docker.DotNet.Enhanced` in `Directory.Packages.props`
   (CPM) and `src/DockYarp.Docker/DockYarp.Docker.csproj`.
2. Update `DockerContainerSource.cs`'s client construction from `DockerClientConfiguration`/`CreateClient()`
   to `DockerClientBuilder`/`.Build()`, matching DockYarp's current endpoint-resolution behavior (verify
   against `Docker:HostAddress`/named-pipe/unix-socket handling already covered by existing e2e tests).
3. Audit all `Docker.DotNet.Models.*` types DockYarp consumes (containers, networks, events) for breaking
   changes between 3.125.15 and 4.3.3 — the fork's own changelog/release notes are the primary source, not
   assumption.
4. Re-run the full e2e suite (`docs/testing.md` coverage map) to confirm discovery still works end-to-end
   against a real Docker daemon.
5. Re-run a throwaway `-p:PublishAot=true` publish (same approach as `investigate-aot-build`) and confirm
   the `Newtonsoft.Json`/`Docker.DotNet`-attributed warnings are gone.

## Acceptance criteria (→ scenarios)

- **WHEN** DockYarp discovers containers via the Docker API **THEN** existing discovery unit/integration/e2e
  tests pass unchanged in behavior (only the client construction and package reference differ).
- **WHEN** a Native AOT publish is attempted **THEN** no IL2xxx/IL3xxx warning traces back to
  `Docker.DotNet`'s own code or a `Newtonsoft.Json` dependency.

## Notes / risks / references

- This is the item most likely to surface real breaking-change friction (major version jump, restructured
  packages, different construction API) — budget for it accordingly; it is not a version bump.
- Combined with [[fix-yamldotnet-aot-trim]] and [[migrate-dashboard-to-razorslices]], this removes every
  warning source `investigate-aot-build` classified as blocking — but Native AOT adoption itself is a
  separate decision to make once all three land and the spike's measurement is re-run (see that change's
  `design.md`, archived under `openspec/changes/archive/`).
- Verify `Docker.DotNet.Enhanced`'s maintenance/API stability at propose time — it is a smaller community
  fork, not an official Microsoft/dotnet-foundation package.
- Refs: `src/DockYarp.Docker/Discovery/DockerContainerSource.cs`, `Directory.Packages.props`.
