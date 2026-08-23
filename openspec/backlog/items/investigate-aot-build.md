---
id: investigate-aot-build
capability: deployment
agent: AG-DEP
tier: C-spike
priority: low
nginx-proxy: n/a (DockYarp packaging/perf)
provenance: 2026-08-14 user backlog discussion (is a Native AOT build of DockYarp possible?)
status: backlog
---

## Why
A Native AOT build could cut DockYarp's startup time and image size (no JIT, self-contained trimmed native binary).
Worth knowing whether it is feasible — and if not, which pragmatic alternative gets most of the benefit.

## Assessment (2026-08-14 — likely NOT feasible today; spike to confirm)
Native AOT forbids runtime reflection/codegen and requires the whole dependency graph to be trim/AOT-safe. DockYarp's
key dependencies are **not** AOT-annotated:
- **YARP** (`Yarp.ReverseProxy`) — does not advertise `IsAotCompatible`; relies on configuration binding + reflection.
  This is the primary blocker. (MS docs: ASP.NET MVC is ❌, Minimal APIs only *partial* for Native AOT; YARP isn't
  listed as supported.)
- **Docker.DotNet** (JSON via reflection), **Certes** (ACME), **OpenTelemetry** exporters — reflection-heavy.
- Reflection-based configuration binding (`GetSection().Get<T>()`) across the app (mitigable with the source-generated
  binder, but every dep must still be safe).

⇒ Expect Native AOT to be blocked by YARP + Docker.DotNet. Do **not** commit to AOT before the spike confirms.

## Proposed change (sketch — a spike, then a recommendation)
1. Set `PublishAot`/trimming analyzers on a throwaway branch; publish and **count the AOT/trim warnings** from YARP,
   Docker.DotNet, Certes, OTEL. Confirm the blocker(s).
2. If blocked (expected), pivot the recommendation to the **pragmatic wins that need no AOT**:
   - **ReadyToRun (R2R)** (`PublishReadyToRun=true`) — faster startup, reflection-safe, no trimming constraints.
   - **Trimming analysis only** (measure, likely can't fully trim due to the same deps).
   - **InvariantGlobalization**, **single-file**, tiered-PGO — small startup/size wins.
   - A smaller base image (already tracked via `add-base-image-rebuild`).
3. Capture the decision (AOT / R2R / status quo) and, if R2R is chosen, wire it into the single Fallout publish
   path ([[nuke-single-build-path]]) behind a flag.

## Acceptance criteria (→ scenarios)
- **WHEN** the spike runs **THEN** the AOT feasibility verdict is recorded (which deps block it, with the warning list).
- **WHEN** AOT is not feasible **THEN** a concrete alternative (e.g. R2R) is recommended with a measured startup/size
  delta, or explicitly deferred.

## Notes / risks / references
- Spike/research item — the output is a **decision + follow-up**, not necessarily shipped code.
- Verify current YARP AOT status at spike time (it may improve). Refs: `Dockerfile`, `build/Build.cs` (`Publish`/
  `DockerImage`/`DockerPublish`), `src/DockYarp.App`.
