## Context

`Test` runs one solution-wide `dotnet test … --filter "TestCategory!=EndToEnd"`. Under that filter,
`AdminApi.Tests` (empty) and `E2E.Tests` (all `EndToEnd`) match zero tests. `dotnet test` over a solution can
then return a non-deterministic non-zero exit code even though the tests that did run passed — the observed
flake.

## Goals / Non-Goals

**Goals:** a deterministic default `Test` gate that needs no Docker and never flakes on no-match projects.

**Non-Goals:** changing `E2E`/`Release`; changing what is tested (same unit/integration coverage).

## Decisions

- **Exclude the e2e suite by project, not by filter.** `Test` runs `dotnet test` for each
  `tests/**/*Tests.csproj` except `DockYarp.E2E.Tests`. The non-e2e projects contain only non-`EndToEnd`
  tests, so no category filter is needed, and no project matches zero tests. `E2E` still runs
  `DockYarp.E2E.Tests` with `TestCategory=EndToEnd`.
- **Remove the empty `AdminApi.Tests`.** It holds no tests; keeping it would make the per-project run fail
  (a project with zero tests exits non-zero). The admin API is covered by `IntegrationTests` over HTTP, the
  same convention already used for `DockYarp.App` (no `App.Tests`).
- **Glob pattern `*Tests.csproj`** (no leading dot) so it also matches `DockYarp.IntegrationTests.csproj`.

## Risks / Trade-offs

- Per-project invocation spawns one `dotnet test` per project instead of one solution run; negligible cost and
  it makes the exit code deterministic. New test projects are picked up automatically by the glob.
- Removing `AdminApi.Tests` drops a (dead) per-source-project test project; acceptable, mirroring `App`.

## Migration Plan

Build tooling + project layout only; no product or behaviour change. `E2E` and `Release` targets are untouched.

## Open Questions

- None.
