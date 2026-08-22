## Context

See proposal.md for motivation and the exact file-level diff shape. Current Nuke surface, verified directly
(not assumed from the backlog stub) at propose time:
- `build/_build.csproj`: `<PackageReference Include="Nuke.Common" Version="10.1.0" />` with an **explicit
  `Version=`** attribute — a deliberate exception to this project's CPM rule, scoped to `build/` by its own
  `Directory.Build.props`/`.targets` stop-files (per AGENTS.md's guardrail: do not touch those files). Fallout's
  own package reference will carry the same pattern; this is not something to "fix" during migration.
- `build/Build.cs` (10 `Nuke.*` references: `using` directives + `: NukeBuild`), `build/Configuration.cs` (1
  `using Nuke.Common.Tooling;`). `build/VersionDetails.cs` carries no Nuke reference.
- `build.ps1`/`build.sh`: a **customized** bootstrap script, not Nuke's canonical generated template — it never
  shells out to a `dotnet nuke` CLI; it does `dotnet build $BuildProjectFile` then
  `dotnet run --project $BuildProjectFile -- $BuildArguments` directly. It does reference `.nuke/temp` and an
  optional `NUKE_ENTERPRISE_TOKEN`/`nuke-enterprise` NuGet source block.
- `.nuke/`: `build.schema.json`, `parameters.json`, `temp/` (gitignored working directory).
- **Every CI workflow already calls `./build.sh <Target>`, never `dotnet nuke` directly** — confirmed by
  grepping all 5 workflow files. This means the migration guide's documented manual step ("CI YAML files are not
  rewritten by `fallout-migrate`; if your CI uses `dotnet nuke` directly … change those by hand") is **moot
  here** — there is nothing to change in CI YAML beyond cosmetic `# Nuke …` comments.

## Goals / Non-Goals

**Goals:**
- Migrate the build orchestrator from Nuke to Fallout with zero behavior change from any CI-consumer's
  perspective (`./build.sh Test`, `./build.sh DockerPublish …`, etc. keep working identically).
- Verify locally before touching anything CI-facing: a real `./build.ps1 Compile`/`Test` run must succeed
  post-migration before any workflow file is touched (even for comment-only edits).

**Non-Goals:**
- Adopting any new Fallout-only feature (its plugin architecture, enterprise CI/CD focus per its roadmap) — this
  change is a like-for-like rename, not a feature adoption.
- Touching `build/Directory.Build.props`/`.targets` (the stop-files) or `.config/dotnet-tools.json` (GitVersion,
  an unrelated tool) — neither carries Nuke-specific content requiring a change.
- A full real CI (GitHub Actions) validation run within this change — Nuke/Fallout's own target graph and
  guardrails are orchestrator-agnostic (MSBuild-level), so a real local `./build.ps1 Test`/`E2E` run is the
  meaningful proof; a real CI push is deferred to whenever this branch's normal PR/push flow happens, not a
  blocking task here.

## Decisions

**Use the official `fallout-migrate` tool (dry-run first), not a hand-rewrite.**

Rationale: the tool is documented as idempotent with a `--dry-run` mode, and the fork's own guide states the 1:1
namespace-swap is "the only structural change" — hand-rewriting 10+ scattered references risks a missed spot
(e.g. `Directory.Packages.props`'s `PackageVersion`, which the guide's own table doesn't explicitly enumerate
since it focuses on `*.csproj` `PackageReference`s — CPM's split file needs its own check, see Risks).
`--dry-run` first specifically because `build.ps1`/`build.sh` here are **customized**, not the canonical
Nuke-generated template the tool most likely targets — inspect its proposed diff before trusting it blindly.

**If `fallout-migrate` doesn't correctly handle the customized `build.ps1`/`build.sh` (no `dotnet nuke` CLI
form to rewrite), fix those two files by hand** using the guide's own manual-migration table as the reference
(`dotnet nuke` → `dotnet fallout` is not applicable here since neither script calls that; only
`.nuke/temp` → `.fallout/temp` and dropping the `NUKE_ENTERPRISE_TOKEN` block apply).

**`Directory.Packages.props`'s `PackageVersion` entry is migrated manually if `fallout-migrate` only touches
`*.csproj` `PackageReference`s** — confirmed as a real gap risk in Decisions above, checked explicitly as its
own task rather than assumed covered.

## Risks / Trade-offs

- [Risk] `fallout-migrate` might not recognize this repo's customized bootstrap scripts and skip rewriting them,
  silently leaving `build.ps1`/`build.sh` referencing `.nuke/temp` after `.nuke/` → `.fallout/` renames it away
  (a temp-directory path that would then not exist, though `New-Item -Force`/`mkdir -p` on first use self-heals
  this — not a hard failure, just wrong-looking paths). → Mitigation: dry-run inspection first (see Decisions);
  manual fix if needed.
- [Risk] CPM's `Directory.Packages.props` might be missed since it's not a plain `*.csproj`. → Mitigation:
  explicit manual-check task, not assumed covered by the tool.
- [Risk] Fallout is young (created 2026-05) relative to Nuke's maturity — 198 open issues is non-trivial for a
  fork this age. → Mitigation: this project's actual target-graph usage is narrow (Compile/Test/E2E/DockerImage/
  DockerPublish/Docs, standard `DependsOn`/`[Parameter]`/typed tool tasks — `DockerTasks`/`DotNetTasks`/
  `NpmTasks`/`GitVersionTasks`), squarely within the guide's "structurally unchanged" claim; a real local build+
  test run is this change's own verification, not just trusting the guide.
- [Risk] If the migration breaks something subtle only visible in CI (a GitHub-hosted runner's environment
  differs from local), it wouldn't be caught by this change's local-only verification. → Mitigation: accepted per
  Non-Goals — the next real push/PR against this branch exercises CI naturally; not worth blocking this change on
  a synthetic CI dry-run when the real one is imminent regardless.

**Correction found during implementation**: the planned `--dry-run` inspection step was accidentally skipped —
`fallout-migrate --version` (meant as an install sanity-check) isn't a recognized flag, and the tool ran the
real migration instead of printing a version. Recovered by inspecting the actual `git diff` immediately after
(equivalent information, just obtained post-hoc rather than pre-hoc) — the tool's real output confirmed every
flagged risk from Decisions was correct: it silently skipped `Directory.Packages.props` (fixed by hand) and left
the `NUKE_ENTERPRISE_TOKEN` block in `build.ps1`/`build.sh` untouched (also fixed by hand), while correctly
handling the customized scripts' `.nuke/temp` → `.fallout/temp` rewrite. No repeat run was needed — the tool is
documented idempotent and the real diff was already in the working tree, reviewable exactly like a dry-run would
have shown.
