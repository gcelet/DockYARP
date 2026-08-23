## Why

A real `-p:PublishAot=true` spike run at the end of `migrate-dashboard-to-razorslices` measured 170 total
AOT/trim warnings; ~28 of those are DockYarp's own code, not a third-party dependency — genuinely fixable,
just never in scope for any of the 3 completed AOT-prep items (none targeted `System.Text.Json` usage
outside their own migration). `src/DockYarp.AdminApi/AdminEndpoints.cs` (6 call sites) and
`src/DockYarp.App/StaticConfig/StaticConfigProvider.cs` (1 call site) use the reflection-based
`System.Text.Json` API without a `JsonSerializerContext`. Unlike the Certes/Newtonsoft.Json bucket
([[investigate-certes-aot-alternative]]), this is .NET's own serializer with first-class source-generator
support built for exactly this — no third-party blocker.

## What Changes

- Add two `JsonSerializerContext` partial classes (one per project — `DockYarp.AdminApi` and
  `DockYarp.App`, confirmed via `dotnet-inspect` to avoid needing a shared dependency neither project
  currently has): `[JsonSerializable(typeof(...))]` for every type each project's JSON call sites need.
- `AdminEndpoints.cs`'s 6 `Results.Json(...)` calls switch to the `Results.Json<TValue>(TValue, JsonSerializerContext, ...)`
  overload (confirmed real via `dotnet-inspect`), OR — the broader, more idiomatic fix, covering
  `BadRequest`/`NotFound` too — the context is registered once via
  `services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, ...))` in
  `ObservabilityServiceCollectionExtensions.AddDockYarpObservability`, so every `Results.*` call across the
  app automatically resolves types through it (decided during design, see `design.md`).
- **BREAKING** (internal only): the two anonymous-object error responses in `AdminEndpoints.cs`
  (`new { error = "..." }`, `new { host, path, matched = false }`) become named records
  (`AdminApiModels.ErrorView`, `AdminApiModels.ResolveNotFoundView`) — anonymous types cannot be referenced
  by a `[JsonSerializable(typeof(...))]` attribute, so this is a required, not optional, part of the fix. The
  JSON shape returned to callers is unchanged (same property names/values), only the C# type backing it is
  now named.
- `StaticConfigProvider.cs`'s `JsonSerializer.Deserialize<StaticConfigFile>(...)` call switches to pass the
  new context's options instead of the current ad-hoc `SerializerOptions` instance.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none — pure implementation swap, zero observable behavior change to API responses; `skip_specs: true` set
in `.openspec.yaml`)

## Impact

- **Code**: `src/DockYarp.AdminApi/AdminEndpoints.cs`, `src/DockYarp.AdminApi/AdminApiModels.cs` (2 new
  named types), a new `src/DockYarp.AdminApi/AdminApiJsonContext.cs`;
  `src/DockYarp.App/StaticConfig/StaticConfigProvider.cs`, a new
  `src/DockYarp.App/StaticConfig/StaticConfigJsonContext.cs`;
  `src/DockYarp.App/Observability/ObservabilityServiceCollectionExtensions.cs` (if the global-registration
  design is chosen). Drive-by doc fix: that file's own XML doc still says "the admin dashboard's Razor Pages
  services" — stale since `migrate-dashboard-to-razorslices`, fixed here per the project's own doc-audit
  habit since this change touches the same file anyway.
- **Tests**: existing `AdminApiIntegrationTests`/`AdminObservabilityTests` (JSON response shape, status
  codes) pass unchanged — the JSON output is byte-identical, only the serialization mechanism differs.
- **AOT**: removes the last DockYarp-owned warning source (~28 of the 170 remaining after
  `migrate-dashboard-to-razorslices`). The only bucket left after this lands is Certes/Newtonsoft.Json
  (~141), tracked separately in [[investigate-certes-aot-alternative]].
