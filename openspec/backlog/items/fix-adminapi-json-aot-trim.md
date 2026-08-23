---
id: fix-adminapi-json-aot-trim
capability: admin-api
agent: AG-AA
tier: A-structural
priority: low
nginx-proxy: n/a (internal finding — AOT/trim readiness, from migrate-dashboard-to-razorslices's own AOT spike)
provenance: 2026-08-23 migrate-dashboard-to-razorslices's real -p:PublishAot=true spike (post-migration measurement)
status: backlog
---

## Why

After all 3 AOT-prep items opened by `investigate-aot-build` landed
([[fix-yamldotnet-aot-trim]], [[migrate-to-docker-dotnet-enhanced]],
`migrate-dashboard-to-razorslices`), a real `-p:PublishAot=true` spike measured 170 remaining warnings
(down from 382 before the Dashboard migration). Of those, **28 are DockYarp's own code**, not a third-party
dependency — genuinely fixable, but never in scope for any of the 3 completed items (none targeted
`System.Text.Json` usage in `AdminApi`/`StaticConfig`):

- `src/DockYarp.AdminApi/AdminEndpoints.cs` — 6 call sites (lines ~36, 37, 38, 39, 58, 64) using
  `Results.Json<TValue>(value, options)` without a `JsonSerializerContext`, each producing an `IL2026`
  (trim) + `IL3050` (AOT) warning pair.
- `src/DockYarp.App/StaticConfig/StaticConfigProvider.cs` — 1 call site (line ~60) using
  `System.Text.Json.JsonSerializer.Deserialize<TValue>(json, options)` without a `JsonSerializerContext`,
  same warning pair.

Unlike the Certes/Newtonsoft.Json bucket (see [[investigate-certes-aot-alternative]]), this is
System.Text.Json — .NET's own serializer, with first-class source-generator support
(`JsonSerializerContext`/`[JsonSerializable]`) built for exactly this AOT/trim scenario. No third-party
blocker; this is pure DockYarp-side follow-through.

## Assessment (2026-08-23)

Fix shape is standard and well-documented: define a `[JsonSerializable(typeof(T))]`-annotated
`JsonSerializerContext` partial class covering every type these two files serialize/deserialize
(`AdminApiModels.VersionView`/`RouteView`/`ClusterView`/`CertView`/`ResolveView`/`HealthView` for
`AdminEndpoints.cs`; whatever `StaticConfigProvider.Read` deserializes — the static YARP config shape — for
the other file), then pass it via the `JsonSerializerOptions.TypeInfoResolver`
(or the `Results.Json(value, jsonTypeInfo)` overload directly) instead of the reflection-based default.

## Proposed change (sketch)

1. Add a `DockYarpJsonSerializerContext` (or two, one per project if a shared one crosses an unwanted
   dependency) with `[JsonSerializable(typeof(...))]` for every type these two files touch.
2. Update `AdminEndpoints.cs`'s 6 `Results.Json(...)` calls to pass the source-generated `JsonTypeInfo`.
3. Update `StaticConfigProvider.cs`'s `JsonSerializer.Deserialize<T>` call to pass the source-generated
   context's options.
4. Re-run a throwaway `-p:PublishAot=true` publish (same approach as the 3 prior AOT-prep items — see
   `vs-cpp-toolchain-native-aot` for this machine's setup) and confirm these ~28 warnings are gone, with no
   new ones introduced.

## Acceptance criteria (→ scenarios)

- **WHEN** the admin API/static-config endpoints are exercised **THEN** existing
  `AdminApiIntegrationTests`/`DockYarp.App` static-config tests pass unchanged in behavior (only the
  serialization mechanism differs).
- **WHEN** a Native AOT publish is attempted **THEN** no `IL2xxx`/`IL3xxx` warning traces back to
  `AdminEndpoints.cs` or `StaticConfigProvider.cs`.

## Notes / risks / references

- Small, bounded, no third-party dependency risk — the most straightforward of the AOT-follow-up items.
- Refs: `src/DockYarp.AdminApi/AdminEndpoints.cs`, `src/DockYarp.App/StaticConfig/StaticConfigProvider.cs`,
  `migrate-dashboard-to-razorslices`'s archived `tasks.md` (full warning breakdown, archived under
  `openspec/changes/archive/`).
