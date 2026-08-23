---
id: fix-yamldotnet-aot-trim
capability: docker-discovery
agent: AG-DD
tier: A-structural
priority: low
nginx-proxy: n/a (internal finding — AOT/trim readiness, from investigate-aot-build)
provenance: 2026-08-23 investigate-aot-build spike (Native AOT feasibility investigation)
status: backlog
---

## Why

The `investigate-aot-build` spike found that `DockYarp.Docker.Labels.MultiportParser`'s use of
`YamlDotNet.Serialization.DeserializerBuilder` (reflection-based) is responsible for ~36 of the 414
trim/AOT warnings emitted by a Native AOT publish — the second-largest warning source after the Dashboard's
Razor Pages usage ([[migrate-dashboard-to-razorslices]]). Unlike the spike's other findings, this one is
fixable entirely on DockYarp's side today: YamlDotNet ships an official AOT-safe source generator, no
upstream release or extensibility gap is blocking it. Fixing this moves DockYarp measurably closer to a
safe Native AOT publish, even though the remaining `Docker.DotNet` blocker (see Notes) means AOT itself
stays out of reach until that separately resolves.

## Assessment (2026-08-23)

`YamlDotNet` provides `Vecc.YamlDotNet.Analyzers.StaticGenerator`, a source generator that replaces the
reflection-based `DeserializerBuilder`/`SerializerBuilder` with `StaticDeserializerBuilder`/
`StaticSerializerBuilder`, backed by a user-authored class inheriting `YamlDotNet.Serialization.StaticContext`
and decorated `[YamlStaticContext]`, with `[YamlSerializable]` on every type that needs (de)serialization
support (see <https://andrewlock.net/using-the-yamldotnet-source-generator-for-native-aot/>). Registering a
type `T` automatically covers `T[]`, `IEnumerable<T>`, `List<T>`, and `Dictionary<string, T>` — but every
custom type reachable from label parsing, including transitively referenced ones, must be registered
explicitly.

## Proposed change (sketch)

1. Add the `Vecc.YamlDotNet.Analyzers.StaticGenerator` package (`Directory.Packages.props`, CPM) to
   `DockYarp.Docker`.
2. Enumerate every type `MultiportParser` (and any other YAML-parsing code in `DockYarp.Docker.Labels`)
   deserializes into, and register them on a `[YamlStaticContext]`-annotated static-context class.
3. Replace `new DeserializerBuilder()` with `new StaticDeserializerBuilder(StaticContext.Instance)` (adjust
   naming to match the chosen context class), preserving existing naming-convention/type-converter
   customization.
4. Re-run a throwaway `-p:PublishAot=true` publish (same approach as `investigate-aot-build`) and confirm
   the YamlDotNet-attributed warnings are gone.

## Acceptance criteria (→ scenarios)

- **WHEN** container labels are parsed via the static-context deserializer **THEN** existing label-parsing
  unit tests (multi-port, and any other YAML-shaped labels) still pass unchanged.
- **WHEN** a Native AOT publish is attempted **THEN** no IL2xxx/IL3xxx warning traces back to
  `YamlDotNet.Serialization.DeserializerBuilder` or its reflection path.

## Notes / risks / references

- This item alone does **not** unblock Native AOT — it removes one of three warning sources. The other two
  are [[migrate-dashboard-to-razorslices]] (the Dashboard's Razor Pages usage) and
  [[migrate-to-docker-dotnet-enhanced]] (Docker.DotNet's own `Newtonsoft.Json`/reflection surface — no
  longer believed to be an unresolvable upstream blocker; see that item). Native AOT adoption itself is a
  separate decision to make once all three land.
- Refs: `src/DockYarp.Docker/Labels/MultiportParser.cs`, `investigate-aot-build`'s `design.md`
  (`## Spike Result`, archived under `openspec/changes/archive/`).
