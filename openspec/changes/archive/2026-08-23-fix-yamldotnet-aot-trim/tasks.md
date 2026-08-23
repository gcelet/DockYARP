## 1. AOT/trim-safe YAML deserialization (AG-DD)

- [x] 1.1 Attempted the source-generator approach first (`Vecc.YamlDotNet.Analyzers.StaticGenerator`) as
      planned. **Real, confirmed upstream generator bugs ruled it out, not assumed**: (a) a nested
      `StaticContext` is silently ignored by the generator — every `StaticContext` method throws
      `NotImplementedException` at runtime with no compile-time signal (matches a live community report,
      [dotnet/YamlDotNet#1124](https://github.com/aaubry/YamlDotNet/issues/1124)); fixed by making the
      context a top-level class. (b) Even fixed, the generator crashes
      (`IndexOutOfRangeException` in `TypeFactoryGenerator`) on any explicit
      `[YamlSerializable(typeof(Dictionary<,>))]` registration — reproduced independently with a
      single-level `Dictionary<string, PathSpec>` and the actual two-level
      `Dictionary<string, Dictionary<string, PathSpec>>` shape, both nested and top-level. Without an
      explicit registration the generator compiles but silently fails to deserialize the two-level
      dictionary (4 tests failing with empty results, no crash). This is a genuine, unresolved limitation
      in the generator (18.1.0, the current release) for this exact shape — not a config mistake.
- [x] 1.2 **Switched to a hand-written `StaticContext`** (`MultiportYamlContext.cs`, top-level, next to
      `MultiportParser.cs`): overrides `IsKnownType`/`GetTypeResolver`/`GetFactory`/`GetTypeInspector`
      directly against YamlDotNet's own public primitives (`StaticObjectFactory`, `TypeInspectorSkeleton`,
      the built-in `StaticTypeResolver`) — no reflection, no source generator, ~150 lines covering exactly
      the 3 types this parser needs (`PathSpec`, and the two dictionary levels). `PathSpec` changed from
      `private` to `internal` so the (now top-level, no longer nested) context can reference it.
- [x] 1.3 Replaced `new DeserializerBuilder()` with `new StaticDeserializerBuilder(new MultiportYamlContext())`,
      preserving `.WithNamingConvention(CamelCaseNamingConvention.Instance)` and
      `.IgnoreUnmatchedProperties()`, verified by the existing `MultiportParser`/`ContainerMapper` multiport
      unit tests passing unchanged (no test content changes) — 151/151 DockYarp.Docker.Tests green.
      `Vecc.YamlDotNet.Analyzers.StaticGenerator` removed from `Directory.Packages.props` and
      `DockYarp.Docker.csproj` (unused — the hand-written context supersedes it).

## 2. AOT warning confirmation (AG-DD, AG-DEP)

- [x] 2.1 Ran a throwaway `-p:PublishAot=true` publish (same approach as `investigate-aot-build`'s spike)
      and confirmed no warning traces back to `YamlDotNet.Serialization.DeserializerBuilder` or
      `MultiportParser`'s reflection path — grepped the publish output for `MultiportParser`/`YamlDotNet`:
      zero matches. Total warning count dropped from 414 (the `investigate-aot-build` baseline) to 379 (-35,
      matching the ~36 originally attributed to this code path).

## 3. Full validation (AG-DD)

- [x] 3.1 `./build.ps1 Test` (or `./build.sh Test`) green — full solution build clean, 151/151
      DockYarp.Docker.Tests green as part of the full suite, no test content changes needed (this change is
      a pure implementation swap).
