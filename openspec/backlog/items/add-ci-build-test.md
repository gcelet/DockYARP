---
id: add-ci-build-test
capability: deployment
agent: AG-DEP
tier: A-structural
priority: high
status: backlog
nginx-proxy: (internal — project CI, no nginx-proxy parity)
provenance: 2026-08-06 CI/ops backlog expansion
---

## Why
There is **no CI**: pull requests and pushes are not built or tested automatically, so a regression (or a broken
build, which is a red build here because warnings are errors) can land unnoticed. This is the **foundation** for
every other CI/ops item (image publish, Renovate PR validation, security scans all gate on a green build).

## Current state
- Build logic exists as a Nuke project (`build/Build.cs`) driven by `build.ps1`/`build.sh`: `Restore` → `Compile`
  → `Test` (unit + integration; E2E excluded — it needs Docker). `TreatWarningsAsErrors=true`, 6 strict analyzers.
- No `.github/workflows/`. The GitHub repo (`gcelet/DockYARP`) does not exist yet.

## Proposed change (sketch)
- Add `.github/workflows/ci.yml` triggered on `pull_request` and `push` to `main`:
  - `actions/checkout`; set up .NET from `global.json` (`actions/setup-dotnet` with `global-json-file`).
  - Cache NuGet (`~/.nuget/packages` keyed on `Directory.Packages.props` + `global.json`).
  - Run `./build.sh Test` on `ubuntu-latest` (the Nuke `Test` gate: compile whole solution + run unit/integration,
    no Docker). Warnings-as-errors already fail the build.
  - Upload the test results / coverage (coverlet) as an artifact.
- Keep it fast + deterministic (E2E is a separate, Docker-gated job — see `add-ci-e2e` if pursued later).

## Acceptance criteria (→ scenarios)
- **WHEN** a pull request is opened or updated **THEN** CI restores, compiles the whole solution, and runs the
  Nuke `Test` gate; a build warning or a test failure fails the check.
- **WHEN** a push to `main` occurs **THEN** the same gate runs.

## Notes / risks / references
- **Repo not created yet** → validate the workflow locally with **`act`** (nektos/act) meanwhile:
  `act pull_request -j build` (Docker required). Also `actionlint` for static workflow linting.
- Pin action versions (or let `add-renovate-bot` manage them). Consider `permissions:` least-privilege.
- Foundation for: `add-ci-image-publish`, `add-renovate-bot`, `add-ci-security-scan`, `add-release-versioning`.
