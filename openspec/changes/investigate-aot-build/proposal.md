## Why

DockYarp's backlog has carried an open question since 2026-08-14: could a Native AOT build cut startup
time and image size? The item's own assessment already names the likely blocker (YARP and Docker.DotNet
are not AOT-annotated), but that has never been confirmed with a real publish and a real warning count.
Running the spike now turns a guess into a recorded decision and closes the loop.

## What Changes

- Publish DockYarp with `PublishAot=true` and trimming analyzers on a throwaway configuration; capture
  every AOT/trim warning emitted by YARP, Docker.DotNet, Certes, and the OpenTelemetry exporters.
- Record the feasibility verdict (blocked / not blocked, with the specific warning list) in this change's
  `design.md`.
- If blocked (the expected outcome), evaluate the pragmatic alternative already named in the backlog item:
  `PublishReadyToRun=true`, measuring the startup/size delta against the current JIT build.
- Capture the final decision (AOT / R2R / status quo) as this change's outcome. No product code ships
  unless R2R is chosen, in which case it is wired into the Fallout `Publish` target behind an explicit
  opt-in.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
(none — this is a research spike; the deliverable is a documented decision, not a behavior change. See
`skip_specs: true` in `.openspec.yaml`.)

## Impact

- No `src/` changes are expected unless the spike recommends R2R, in which case `build/Build.cs`
  (the `Publish` target) gains an opt-in flag.
- No test changes.
- Output: a recorded feasibility verdict and decision in `design.md`, closing the backlog item either
  way.
