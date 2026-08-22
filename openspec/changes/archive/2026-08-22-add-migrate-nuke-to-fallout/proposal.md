## Why

Nuke, this project's entire build/test/publish/release orchestrator, is no longer actively maintained by its
owner (`nuke-build/nuke#1564`, 2025-11-18: the maintainer declined to transfer the repo and invited community
forks; confirmed live at propose time — zero pushes since 2025-12-02, 8.5+ months). Fallout
(`Fallout-build/Fallout`) is the resulting community fork — confirmed active and stable at propose time (pushed
3 days ago, v10.4.0, 133 stars, a documented `fallout-migrate` tool with a `--dry-run` mode). Nothing is broken
today; this is a forward-looking maintenance risk (no upstream security/compatibility fixes), not an incident.

## What Changes

- `build/_build.csproj`: `Nuke.Common` → `Fallout.Common` package reference; `NukeRootDirectory`/
  `NukeScriptDirectory`/`NukeTelemetryVersion` MSBuild properties → their `Fallout*` equivalents (Fallout has no
  telemetry, so the last one is dropped, not renamed).
- `Directory.Packages.props`: `Nuke.Common` `PackageVersion` → `Fallout.Common`.
- `build/Build.cs`, `build/Configuration.cs`: `using Nuke.X.Y;` → `using Fallout.X.Y;`, `: NukeBuild` →
  `: FalloutBuild` (type identifiers other than the base class/interface — `DockerTasks`, `DotNetTasks`,
  `NpmTasks`, `GitVersionTasks`, `[Parameter]`, `Solution`, `GitRepository`, … — are unchanged per the fork's own
  1:1 namespace-swap guarantee).
- `build.ps1`/`build.sh`: the `NUKE_ENTERPRISE_TOKEN`/`nuke-enterprise` NuGet source block is removed (Fallout
  has no enterprise tier); `.nuke/temp` → `.fallout/temp`.
- `.nuke/` → `.fallout/` (directory rename, contents preserved: `build.schema.json`, `parameters.json`).
- `.github/workflows/*.yml`: **no functional change** — every workflow already calls `./build.sh <Target>`, never
  `dotnet nuke` directly, so `fallout-migrate`'s documented "CI YAML isn't rewritten, fix `dotnet nuke` calls by
  hand" caveat doesn't apply here. Only the workflows' own `# Nuke ...` comments are reworded for accuracy.
- `.config/dotnet-tools.json`, `build/Directory.Build.props`/`.targets` (the stop-files): untouched — GitVersion
  is a separate tool unrelated to Nuke/Fallout, and the stop-files carry no Nuke-specific content.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
(none — pure build-tooling swap, zero DockYarp runtime behavior change; `skip_specs: true` is set in this
change's `.openspec.yaml`, matching the backlog item's own framing: "internal build tooling, not a proxy
feature. No `parity.md` row.")

## Impact

- `build/_build.csproj`, `build/Build.cs`, `build/Configuration.cs`, `build/VersionDetails.cs` (verified to carry
  no `Nuke.*` reference — needs no edit, listed for completeness).
- `Directory.Packages.props`.
- `build.ps1`, `build.sh`.
- `.nuke/` → `.fallout/` (renamed).
- `.github/workflows/{ci,image,base-image-refresh,codeql,docs}.yml` (comment wording only).
- Every CI workflow's actual invocation (`./build.sh Test`, `./build.sh DockerPublish …`, `./build.sh Compile`,
  `./build.sh Docs`) keeps working unchanged from the CI-consumer's perspective — same target names, same
  parameters, same `Directory.Build.props`/CPM/`TreatWarningsAsErrors` guardrails layered on top (all MSBuild-
  level, orthogonal to which orchestrator owns `Execute<Build>`).
