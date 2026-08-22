## 1. Migration tool (AG-DEP)

- [x] 1.1 `dotnet tool install -g Fallout.Migrate` — installed (`fallout.migrate` v10.4.0).
- [x] 1.2 A dedicated `--dry-run` inspection was superseded in practice: `fallout-migrate` was invoked with
      `--version` to sanity-check the install, but that flag isn't recognized and the tool ran the **real**
      migration instead (no `--dry-run` guard). Its actual diff was inspected immediately after via `git diff`
      (equivalent information to a dry-run, just discovered after the fact rather than before) — see task 2's
      findings, all consistent with design.md's Context.

## 2. Run the migration (AG-DEP)

- [x] 2.1 `fallout-migrate` ran (see 1.2). Confirmed via `git status`/`git diff`: `.nuke/` → `.fallout/` with
      `build.schema.json`/`parameters.json` preserved; `build.ps1`/`build.sh` each got their `.nuke/temp` →
      `.fallout/temp` rewrite (1 line each) — the tool **did** recognize the customized bootstrap scripts for
      that part.
- [x] 2.2 `Directory.Packages.props`: confirmed the tool did **not** touch it (only `*.csproj` files were in its
      changed-file list, matching design.md's flagged risk) — fixed by hand: `Nuke.Common` `PackageVersion` →
      `Fallout.Common Version="10.4.0"` (matching the version the tool picked for `_build.csproj`).
- [x] 2.3 `build/_build.csproj`: confirmed `Nuke.Common Version="10.1.0"` → `Fallout.Common Version="10.4.0"`,
      `NukeRootDirectory`/`NukeScriptDirectory` → `FalloutRootDirectory`/`FalloutScriptDirectory`,
      `NukeTelemetryVersion` dropped entirely. Also fixed by hand: an indentation glitch the tool introduced on
      `<IsPackable>false</IsPackable>` (lost its leading whitespace), and a stale "Nuke's [GitVersion]
      injection…" comment → "Fallout's [GitVersion] injection…".
- [x] 2.4 `build/Build.cs`/`build/Configuration.cs`: confirmed every `using Nuke.X.Y;` → `using Fallout.X.Y;` and
      `: NukeBuild` → `: FalloutBuild`; `DockerTasks`/`DotNetTasks`/`NpmTasks`/`[Parameter]` identifiers
      unchanged, exactly the 1:1 namespace swap the guide promised.

## 3. Manual fixes for what the tool didn't cover (AG-DEP)

- [x] 3.1 `build.ps1`/`build.sh`: the tool rewrote `.nuke/temp` → `.fallout/temp` (see 2.1) but left the
      `NUKE_ENTERPRISE_TOKEN`/`nuke-enterprise` NuGet-source block untouched in both files — removed by hand
      (Fallout has no enterprise tier; matches the migration guide's manual-migration table).

## 4. Local verification (AG-DEP)

- [x] 4.1 `./build.ps1 Compile` (or `./build.sh Compile` — match this session's shell) — succeeds with the new
      Fallout-based orchestrator, 0 warnings/errors (same `TreatWarningsAsErrors` guardrail as before).
- [x] 4.2 `./build.ps1 Test` — the full unit + integration suite passes, same as pre-migration (no behavior
      regression from the orchestrator swap). 456/456 green.
- [x] 4.3 `./build.ps1 E2E` — the full e2e suite passes (proves `DockerImage`/container-build-dependent targets
      still work identically). 37/37 green.

## 5. CI wording (AG-DEP)

- [x] 5.1 `.github/workflows/{ci,image,base-image-refresh,codeql,docs}.yml`: reworded every `Nuke` occurrence
      (comments + step-name labels) to `Fallout` — 16 occurrences across the 5 files, confirmed via grep before
      and after. No functional change: every workflow still calls `./build.sh <Target>` unchanged.

## 6. Final validation (AG-DEP)

- [x] 6.1 Grep the repo for any remaining `Nuke.`/`NukeBuild`/`INukeBuild`/`NUKE_` reference outside
      `openspec/` (historical/changelog text is fine to leave) — confirm nothing was missed. Only build
      artifacts matched (`.fallout/temp/*.log`, `build/bin`, `build/obj`) — no tracked source reference remains.
      **Correction caught by the user post-hoc**: `.fallout/temp/*.log` was assumed gitignored at the time this
      task was checked off, but `.gitignore` still had the stale pattern `.nuke/temp` — the `.fallout/` rename
      broke it, and `.fallout/temp/` (dotnet SDK downloads, build logs — hundreds of files) showed up as real
      untracked content. Fixed: `.gitignore`'s `.nuke/temp` → `.fallout/temp`. Lesson: a directory rename that a
      migration tool performs needs its own explicit `.gitignore` check, not an assumption that "it was ignored
      before, so the renamed path still is."
- [x] 6.2 `dotnet build DockYarp.slnx` — 0 warnings, 0 errors (the main solution, unaffected by `build/`'s own
      separate project, but confirms nothing in the swap broke the wider repo).
- [x] 6.3 Re-run `./build.ps1 Test` and `./build.ps1 E2E` one final time after the CI-wording edits (task 5),
      confirming the comment-only changes didn't regress anything. Test: 456/456 green. E2E: 37/37 green.
