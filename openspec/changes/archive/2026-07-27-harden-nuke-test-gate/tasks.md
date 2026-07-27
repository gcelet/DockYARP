## 1. Remove the empty test project (AG-DEP)

- [x] 1.1 Delete `tests/DockYarp.AdminApi.Tests/` and its `DockYarp.slnx` entry

## 2. Deterministic Test target (AG-DEP)

- [x] 2.1 Rework `build/Build.cs` `Test` to run `dotnet test` per `tests/**/*Tests.csproj` except the e2e
      project (exclude by project, drop the category filter). `E2E`/`Release` unchanged.

## 3. Docs & validation

- [x] 3.1 `docs/deployment.md`: note the default `Test` runs the unit/integration projects (e2e excluded by project)
- [x] 3.2 `./build.ps1 Test` green on repeated runs (deterministic)
- [x] 3.3 `openspec validate harden-nuke-test-gate --strict`
