## Context

See `proposal.md` for motivation. `MultiportParser`'s only YAML entry point is
`Deserializer.Deserialize<Dictionary<string, Dictionary<string, PathSpec>>>(yaml)`, where `PathSpec` is a
`private sealed class` nested inside `MultiportParser` itself (three settable properties: `Port`, `Dest`,
`Proto`). This is the entire surface that needs source-generated coverage — no other YAML parsing exists
in `DockYarp.Docker`.

## Goals / Non-Goals

**Goals:**
- Replace the reflection-based `DeserializerBuilder` with YamlDotNet's source-generated
  `StaticDeserializerBuilder`, eliminating the `IL3050` warning a Native AOT publish attributes to this
  code path.
- Zero behavior change: identical parse results for identical input, verified by the existing test suite
  passing unchanged.

**Non-Goals:**
- Touching any other DockYarp.Docker label-parsing code — `MultiportParser` is the only YAML consumer.
- Re-running the full `investigate-aot-build` AOT publish spike as part of this change — that measurement
  belongs to whichever change actually attempts adoption; this change only needs to confirm this specific
  warning source is gone (see Acceptance criteria in the backlog item / tasks.md).

## Decisions

- **Hand-written `StaticContext`, not the source generator.** The original plan was to adopt
  `Vecc.YamlDotNet.Analyzers.StaticGenerator` (18.1.0, the current release at the time). It was tried
  first, and ruled out by two independently confirmed, real bugs in the generator itself — not a
  DockYarp-side mistake:
  1. A `StaticContext` nested inside another class (the original plan, to reach the then-`private`
     `PathSpec`) is silently ignored by the generator: it compiles cleanly, but every `StaticContext`
     method throws `NotImplementedException` at runtime with no compile-time signal. This matches a live
     community report, [dotnet/YamlDotNet#1124](https://github.com/aaubry/YamlDotNet/issues/1124) — a
     link the user supplied mid-implementation, pointing at a maintainer comment confirming the context
     must be a top-level class. Fixed by making `MultiportYamlContext` top-level and widening `PathSpec`
     from `private` to `internal` so the sibling type can still reach it.
  2. Even fixed as a top-level class, the generator crashes (`IndexOutOfRangeException` in its own
     `TypeFactoryGenerator`) on any explicit `[YamlSerializable(typeof(Dictionary<,>))]` registration —
     reproduced independently with both the single-level `Dictionary<string, PathSpec>` and the actual
     two-level `Dictionary<string, Dictionary<string, PathSpec>>` shape this parser needs, and separately
     with `List<PathSpec>` (a different failure mode: malformed generated C#, `CS1001`/`CS1002`/`CS0443`).
     That breadth — failing across `Dictionary` and `List` alike — indicates a general gap in the
     generator's generic-collection support, not a shape-specific edge case. It cross-references an open
     upstream issue, [dotnet/YamlDotNet#1023](https://github.com/aaubry/YamlDotNet/issues/1023) ("Support
     'generic' (dotnet runtime types only) AOT"), confirming source-generated coverage of generic
     containers is a known, still-unresolved gap in the project, not something this change could work
     around by adjusting registration syntax. Without an explicit registration the generator compiles but
     silently fails to deserialize the two-level shape (empty results, no exception, no test failure
     signal beyond wrong output).

  With both failure modes confirmed live and neither fixable from the DockYarp side, `MultiportYamlContext`
  (`src/DockYarp.Docker/Labels/MultiportYamlContext.cs`) is hand-written directly against YamlDotNet's own
  public primitives instead: `StaticContext` (`IsKnownType`/`GetTypeResolver`/`GetFactory`/
  `GetTypeInspector`), `StaticObjectFactory`, `TypeInspectorSkeleton`, the built-in
  `TypeResolvers.StaticTypeResolver`, and a small hand-rolled `IPropertyDescriptor` per `PathSpec`
  property. No reflection anywhere in the result — the same AOT/trim goal, reached without the generator.
  `Vecc.YamlDotNet.Analyzers.StaticGenerator` was added to `Directory.Packages.props` and
  `DockYarp.Docker.csproj` during the investigation and removed again once it proved unusable for this
  shape — it is not a dependency of the final code.
- **Registration scope**: `MultiportYamlContext.IsKnownType` recognizes exactly the 3 types this parser
  needs — `PathSpec`, `Dictionary<string, PathSpec>`, and `Dictionary<string, Dictionary<string, PathSpec>>`
  — matching the hand-written factory/inspector, which only need to handle those three cases.
- **Naming convention / unmatched-property tolerance**: confirmed unchanged — `StaticDeserializerBuilder`
  accepts the same `.WithNamingConvention(...)`/`.IgnoreUnmatchedProperties()` fluent calls as
  `DeserializerBuilder` (both implement the same builder pattern).

## Risks / Trade-offs

- [Trade-off accepted] The hand-written context is more code to maintain (~150 lines) than a generator
  attribute would have been, and any future change to `PathSpec`'s shape (a new property, a nested type)
  needs a matching manual update to `MultiportYamlContext`. Accepted because the alternative — the source
  generator — does not currently work for this shape at all (see Decisions above); this is not a stylistic
  preference over the generator, it is the only working option today. If a future YamlDotNet release fixes
  the generic-collection registration bug (upstream issue #1023), revisiting the generator becomes a
  reasonable follow-up, not a requirement.
- [Risk, materialized and resolved] The original risk note here was uncertainty about whether the
  generator's one-level `Dictionary<string, T>` auto-coverage would extend to the two-level shape used by
  `MultiportParser`. That uncertainty is now moot — the generator was ruled out entirely for unrelated
  reasons (the crash bug above) before the auto-coverage question could even be tested.
