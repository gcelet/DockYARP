## Why

The `investigate-aot-build` spike found that `MultiportParser`'s reflection-based
`YamlDotNet.Serialization.DeserializerBuilder` is responsible for ~36 of the 414 trim/AOT warnings a Native
AOT publish emits — the second-largest source. Unlike the spike's other findings, this one has a confirmed,
DockYarp-side-only fix (YamlDotNet's official source generator) with no upstream dependency, so it is worth
doing regardless of when (or whether) DockYarp ultimately adopts Native AOT — it removes real reflection
from a hot discovery-time code path either way.

## What Changes

- `DockYarp.Docker.Labels.MultiportParser` swaps its reflection-based `DeserializerBuilder` for YamlDotNet's
  source-generated `StaticDeserializerBuilder`, backed by a `[YamlStaticContext]`-annotated static context
  registering every type reachable from `VIRTUAL_HOST_MULTIPORTS` parsing (currently just the private
  `PathSpec` record/class).
- No observable behavior change: the same YAML shapes parse to the same `MultiportEntry` results, the same
  naming convention (camelCase) and unmatched-property tolerance are preserved. Purely an internal
  implementation swap — `skip_specs: true`.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
(none — no requirement-level behavior changes.)

## Impact

- `src/DockYarp.Docker/Labels/MultiportParser.cs` only.
- `Directory.Packages.props` gains `Vecc.YamlDotNet.Analyzers.StaticGenerator` (CPM).
- No test behavior changes expected — existing `LabelParserTests`/multiport-parsing unit tests must keep
  passing unchanged; they are the acceptance proof.
- Does **not** unblock Native AOT by itself — see `investigate-aot-build`'s archived `design.md` for the
  other two warning sources (`migrate-dashboard-to-razorslices`, `migrate-to-docker-dotnet-enhanced`).
