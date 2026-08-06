# Design — add-ci-build-test

## Workflow shape
`.github/workflows/ci.yml`, one `build-test` job on `ubuntu-latest`:
- `actions/checkout@v4`.
- `actions/setup-dotnet@v4` with `global-json-file: global.json` — installs a matching SDK. `global.json` is
  relaxed to accept **any installed .NET 10** SDK (`version: 10.0.100`, `rollForward: latestMinor`): the base 10
  floor, rolling to the latest 10.x present, never demanding a specific feature band (which broke on machines
  without the newest SDK and was painful to maintain without Renovate) and never crossing to .NET 11.
- `actions/cache@v4` on `~/.nuget/packages`, keyed on `Directory.Packages.props` + `global.json` (CPM means the
  lockable version set lives there), with an `os`-scoped restore fallback.
- `./build.sh Test` — the Nuke gate (`Restore` → `Compile` whole solution → `Test` unit/integration). `build.sh`
  is executable (git mode 100755). Warnings-as-errors + the 6 analyzers already fail a bad build.

## Why these choices
- **Test gate, not E2E**: `Test` excludes the Aspire E2E suite (it needs Docker/DCP) — keep CI fast and
  daemon-free. A Docker-gated E2E job is a separate future item.
- **`global-json-file`** over a hardcoded version: single source of truth, and Renovate (`add-renovate-bot`) can
  bump the SDK in one place.
- **`concurrency`** cancels superseded runs on the same ref; **`permissions: contents: read`** is least-privilege
  (no writes needed for a build gate).
- Linux runner: `build.sh` (the E2E is run there too, in WSL), and it is the cheapest GitHub runner.

## Validation without the repo
No `.github/workflows` runner locally and the repo isn't created yet. Validate with:
- `actionlint .github/workflows/ci.yml` (static workflow linting), and
- `act pull_request -j build-test` (nektos/act — runs the job in a container; Docker required).
The Nuke `Test` gate itself is already proven on Windows (`./build.ps1 Test`) and Linux (WSL E2E runs compile it).

## Out of scope
- Image publish, release, Renovate, security scans, E2E-in-CI (separate items).
- Matrix across OSes (a single ubuntu build is enough for the gate; add later if needed).
