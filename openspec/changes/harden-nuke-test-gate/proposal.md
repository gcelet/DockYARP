## Why

The Nuke `Test` target intermittently exits non-zero even though every unit/integration test passes. It runs
`dotnet test DockYarp.slnx --filter "TestCategory!=EndToEnd"`, and **two projects have no matching tests**
under that filter — `DockYarp.AdminApi.Tests` (an empty placeholder: only a `.csproj` and a `README`, no
tests) and `DockYarp.E2E.Tests` (all tests are `EndToEnd`). A solution-wide `dotnet test` with a filter
that matches no tests in some projects returns a non-deterministic exit code, so the default gate flakes red.

## What Changes

- **Remove the empty `DockYarp.AdminApi.Tests` project** (dir + `DockYarp.slnx` entry). It contains no
  tests; the admin API's behaviour is already covered by `DockYarp.IntegrationTests` (over HTTP), the same
  way `DockYarp.App` has no `App.Tests`.
- **Rework the Nuke `Test` target** to run `dotnet test` **per test project**, over every `tests/**/*Tests.csproj`
  **except** `DockYarp.E2E.Tests`. The end-to-end suite is thus excluded **by project** (not by a
  filter that matches nothing), so `Test` is deterministic and still needs no Docker. `E2E` and `Release` are
  unchanged.

## Capabilities

### Modified Capabilities
- `deployment`: the default build/test gate runs deterministically — the end-to-end suite is excluded by
  project, and the run does not fail on projects that match no tests.

## Impact

- **Build/tests only**: `build/Build.cs` (`Test` target), `DockYarp.slnx`, removed
  `tests/DockYarp.AdminApi.Tests/`, `docs/deployment.md`. No `src/` change.
- **Owning agent**: AG-DEP.
