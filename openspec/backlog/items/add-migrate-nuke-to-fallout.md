---
id: add-migrate-nuke-to-fallout
capability: deployment
agent: AG-DEP
tier: B-runtime
priority: low
status: backlog
nginx-proxy: n/a (DockYarp build tooling, not a proxy feature)
provenance: 2026-08-18 user report, verified against the primary sources before writing this stub
---

## Why

Nuke (`build/Build.cs`, this project's entire build/test/publish/release pipeline) is no longer actively
maintained by its owner. Confirmed against the primary sources, not assumed:
- [nuke-build/nuke discussion #1564](https://github.com/nuke-build/nuke/discussions/1564) — on 2025-11-18,
  Matthias Koch (the project's actual owner, `@matkoch`) posted an official response citing "the well-known
  OSS sustainability problem" and personal attacks during a vulnerable time. He confirmed he will **not**
  transfer the repository to a successor maintainer ("for security and reputational reasons") and explicitly
  invited the community to fork it under their own name.
- `nuke-build/nuke`'s own repository has had no push since 2025-12-02 (checked live 2026-08-18 — over 8 months
  of inactivity), consistent with that statement.
- [Fallout-build/Fallout](https://github.com/Fallout-build/Fallout) is the community fork that statement led
  to — active (128 stars, last push 2026-08-17), with a documented, tool-assisted migration path.

Nothing is broken today — the pinned Nuke version still builds/tests/publishes DockYarp correctly (verified as
recently as this session's `add-e2e-release-gate` work). This is a forward-looking risk (no upstream security
fixes, no compatibility updates for future .NET SDKs) rather than an active incident.

## nginx-proxy behavior

N/A — internal build tooling, not a proxy feature. No `parity.md` row.

## DockYarp today

`build/Build.cs` targets `NukeBuild`/`INukeBuild` from the `Nuke.Common`/`Nuke.Build` NuGet packages
(`Directory.Packages.props`); `build.ps1`/`build.sh` bootstrap the `_build.csproj` project; `.nuke/` holds the
generated CLI schema. Every CI workflow (`ci.yml`, `image.yml`, `base-image-refresh.yml`, `codeql.yml`,
`docs.yml`) invokes it via `dotnet nuke` / `./build.sh`/`./build.ps1`.

## Proposed change (sketch)

Per [Fallout's own migration guide](https://github.com/Fallout-build/Fallout/blob/main/docs/migration/from-nuke.md):
1. `dotnet tool install -g Fallout.Migrate`, run `fallout-migrate` from the repo root. It automates: package
   references (`Nuke.X` → `Fallout.X`), using directives/namespaces, the base class (`NukeBuild` →
   `FalloutBuild`), MSBuild properties, bootstrap scripts, and the `.nuke/` → `.fallout/` directory rename. It
   also removes the stale Nuke Enterprise NuGet source and strips telemetry config (Fallout has none).
2. Target/DependsOn/Executes/Parameter-based orchestration is structurally unchanged (confirmed in the guide) —
   `Build.cs`'s actual target graph should not need rewriting, only the scaffolding around it.
3. **Manual step the tool does not cover**: every CI workflow's `dotnet nuke`/`./build.sh`/`./build.ps1`
   invocation needs updating to the Fallout equivalent (`dotnet fallout` / renamed wrapper scripts).
4. Verify the guardrails this project layers on top still hold after migration: `TreatWarningsAsErrors`, CPM,
   the `build/Directory.Build.*` stop-files ([[nuke-single-build-path]], [[prefer-nuke-apis]] still apply to
   whatever the Fallout-equivalent typed tool-task APIs are called).

## Acceptance criteria (→ scenarios)

- **WHEN** the migration tool runs against this repo **THEN** `build/Build.cs` compiles and every existing
  target (`Test`, `E2E`, `DockerImage`, `DockerPublish`, `Release`, `Publish`, `Smoke`, …) still resolves and
  runs with unchanged behavior.
- **WHEN** each CI workflow runs post-migration **THEN** it succeeds using the Fallout-equivalent invocation,
  with no behavior change from the user/CI-consumer's perspective.

## Notes / risks / references

- **Not urgent** — nothing is broken; this is a deliberate, plannable migration, not a fire. Reasonable to slot
  in whenever a natural pause exists, not necessarily before a v1.
- Fallout is young (created 2026-05) relative to Nuke's own maturity — worth a light gut-check on its own
  stability/momentum at migration time, not just at stub-writing time.
- Re-verify the migration guide's exact steps at execution time (a young, actively-developed fork's own docs
  may have moved since this stub was written).
