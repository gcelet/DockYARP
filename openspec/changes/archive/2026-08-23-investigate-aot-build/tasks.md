## 1. AOT feasibility spike (AG-DEP)

- [x] 1.1 Publish `src/DockYarp.App` for `linux-x64` with `-p:PublishAot=true -p:TrimmerSingleWarn=false`
      (throwaway, not committed) and capture the full console output. Publish succeeded (exit 0) with 414
      warnings; log captured in `.spike-aot-console.log` (not committed).
- [x] 1.2 Classify every IL2xxx/IL3xxx warning and any hard publish failure by originating package
      (DockYarp code vs. YARP vs. Docker.DotNet vs. Certes vs. OpenTelemetry vs. other), verified by a
      warning-list table with a package column for each entry. See `design.md` `## Spike Result`.
- [x] 1.3 Re-check the current NuGet/AOT-compatibility status of YARP and Docker.DotNet at spike time
      (their release notes / `IsAotCompatible` metadata), verified by citing the checked package version
      and source for each. Checked via `dotnet-inspect` package/library metadata; see `design.md`.

## 2. ReadyToRun fallback measurement (AG-DEP)

- [x] 2.1 If step 1 confirms AOT is blocked, publish the same target with `-p:PublishReadyToRun=true`
      (no trimming) and record the published output size, verified by comparing it against the existing
      `dockyarp:local` JIT image's published size. R2R: 120 MB vs. JIT self-contained baseline: 112 MB
      (R2R is larger, not smaller) — see `design.md` `## Spike Result`.
- [x] 2.2 Measure cold-start time for the R2R publish vs. the current JIT publish (3 runs each, median),
      verified by recording both medians and the delta. R2R ~414 ms vs. JIT ~467 ms (~11% faster); AOT
      ~209 ms measured for reference. See `design.md`.

## 3. Record the decision (AG-DEP)

- [x] 3.1 Append a `## Spike Result` section to `design.md` with the feasibility verdict, the warning
      classification table, and the measured R2R delta (if applicable), verified by the section being
      present and citing concrete numbers/warnings rather than restating the original assessment.
- [x] 3.2 Record the final decision (AOT / R2R / status quo) in the same section, verified by an explicit
      one-line conclusion. Decision: status quo (see `design.md`).
- [x] 3.3 If R2R is the chosen outcome, wire `PublishReadyToRun` into the Fallout `Publish` target
      (`build/Build.cs`) behind an explicit opt-in parameter, verified by `./build.ps1 Publish` still
      succeeding with the flag unset (default unchanged) and succeeding with it set. **Not applicable** —
      R2R was measured but not chosen (status quo decision); no build changes made.
