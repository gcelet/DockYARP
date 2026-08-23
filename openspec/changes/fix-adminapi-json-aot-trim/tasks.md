## 1. AdminApi JSON source generation (AG-AA)

- [x] 1.1 Added `AdminApiModels.ErrorView(string Error)` (positional record) and
      `AdminApiModels.ResolveNotFoundView` (init-property record, not positional — a positional `bool`
      parameter tripped `AV1564`/CSharpGuidelinesAnalyzer, matching the file's own existing mixed style for
      records with a bool-typed member, e.g. `SecurityView`) to `AdminApiModels.cs`, replacing the two
      anonymous objects in `AdminEndpoints.cs`. Verified: `dotnet build` compiles.
- [x] 1.2 Added `src/DockYarp.AdminApi/AdminApiJsonContext.cs`: a `JsonSerializerContext` partial class,
      `public` (not `internal` as first written — `DockYarp.App`, a different assembly, needs to reference
      it for the global registration in 1.3; compile error caught this immediately), with
      `[JsonSerializable(typeof(...))]` for all 8 types.
- [x] 1.3 Added `services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, AdminApiJsonContext.Default));`
      to `ObservabilityServiceCollectionExtensions.AddDockYarpObservability` — this alone covers
      `BadRequest`/`NotFound` (which have no overload accepting a context; empirically confirmed via the
      first AOT spike below that these two already produced zero warnings on their own, unlike the 6
      `Results.Json` calls). Fixed the stale XML doc ("Razor Pages services" → "RazorSlices").
- [x] 1.4 **Design correction found via a real AOT spike, not assumed correct**: after 1.1-1.3, a throwaway
      `-p:PublishAot=true` publish still showed all 28 original warnings, unchanged — the DI-registered
      context has zero effect on which overload the trim/AOT analyzer flags, since overload resolution for
      `Results.Json(value)` happens at COMPILE TIME based on the static call signature, not at runtime via
      DI. Fixed for real: each of the 6 `Results.Json(...)` calls now passes `AdminApiJsonContext.Default`
      explicitly as the second argument (the real `Results.Json<TValue>(TValue, JsonSerializerContext, ...)`
      overload, confirmed via `dotnet-inspect` before this fix). **A second real bug then surfaced via the
      test suite**: passing the context directly makes `Results.Json` use the CONTEXT's own
      `JsonSerializerOptions` (PascalCase by default), not the ambient camelCase options minimal APIs use —
      `AdminApiIntegrationTests.RoutesWithKeyAreSanitized`/`VersionReturnsBuildVersion` failed for real
      (`"RequiresAuth"` instead of `"requiresAuth"`). Fixed: added
      `[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]` to
      `AdminApiJsonContext` to match. `dotnet build DockYarp.slnx` compiles clean. `dotnet test
      DockYarp.slnx` — 515/515 green (0 test content changes needed once the naming policy was fixed —
      confirms the JSON shape really is byte-identical to before, not just assumed).

## 2. StaticConfigProvider JSON source generation (AG-DEP)

- [x] 2.1 Added `src/DockYarp.App/StaticConfig/StaticConfigJsonContext.cs`: `internal` `JsonSerializerContext`
      with `[JsonSerializable(typeof(StaticConfigFile))]` — confirmed the nested `ClusterEntry`/`RouteEntry`/
      `OverrideEntry` types needed no separate attribute (covered transitively, compiled clean first try).
- [x] 2.2 **Design correction, same root cause as 1.4**: `TypeInfoResolver = StaticConfigJsonContext.Default`
      added to the existing `SerializerOptions` field (combined with the ambient `JsonSerializerOptions`
      instance) still left `JsonSerializer.Deserialize<StaticConfigFile>(json, SerializerOptions)` bound to
      the reflection-permissive overload — same compile-time-overload-resolution issue as `Results.Json`.
      Fixed for real: switched to `JsonSerializer.Deserialize(json, StaticConfigJsonContext.Default.StaticConfigFile)`
      (the `JsonTypeInfo<TValue>`-accepting overload), and moved `PropertyNameCaseInsensitive = true` onto
      `StaticConfigJsonContext` itself via `[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]`
      (deserialization-only setting — no naming-policy mismatch risk here, unlike 1.4's serialization-side
      bug, since case-insensitive matching doesn't depend on a naming policy). The now-unused
      `SerializerOptions` field was removed.
- [x] 2.3 `dotnet build DockYarp.slnx` compiles clean. `dotnet test DockYarp.slnx` — 515/515 green, including
      the e2e suite which exercises `StaticConfigProvider` against a real static-config file (no test content
      changes needed).

## 3. Full validation + AOT confirmation (AG-AA, AG-DEP)

- [x] 3.1 `./build.ps1 Test` green — full solution build clean, all tests pass.
- [x] 3.2 Throwaway `-p:PublishAot=true` publish, real and complete (PATH fix + clean `obj`/`bin`, per
      [[vs-cpp-toolchain-native-aot]]). **Real, verified result**: 142 total warnings (down from 170, the
      `migrate-dashboard-to-razorslices` baseline) — exactly the expected -28. Zero warnings trace to
      `AdminEndpoints.cs` or `StaticConfigProvider.cs`, confirmed via a targeted grep, not assumed from the
      total-count delta alone. The only bucket remaining is Certes/Newtonsoft.Json (~141) plus the small BCL/
      BouncyCastle tail, tracked in [[investigate-certes-aot-alternative]] — this item's own scope is fully
      closed.
