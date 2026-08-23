## Context

See `proposal.md` for motivation. Two independent, small call sites: `AdminEndpoints.cs`'s 6 `Results.Json`
calls (DI-resolved `WebApplication`, JSON options flow through ASP.NET Core's own `JsonOptions`), and
`StaticConfigProvider.cs`'s single `JsonSerializer.Deserialize<StaticConfigFile>` call (a private static
`JsonSerializerOptions` field, entirely local, no DI involved). They need different wiring because of that
difference, confirmed by reading both files directly rather than assumed.

## Goals / Non-Goals

**Goals:**
- Zero DockYarp-owned trim/AOT warning remaining in either file.
- Zero observable change to the JSON responses' shape/content or the static-config file's accepted shape.

**Non-Goals:**
- Touching the Certes/Newtonsoft.Json warning bucket — tracked separately, see
  [[investigate-certes-aot-alternative]].
- Enabling Native AOT publish itself for DockYarp — still a separate future decision.

## Decisions

- **`AdminEndpoints.cs`: BOTH a global `ConfigureHttpJsonOptions` registration AND explicit per-call context
  arguments are needed — the original plan (DI registration alone) was wrong, confirmed by a real AOT spike,
  not assumed.** `Results.BadRequest<TValue>(TValue)`/`Results.NotFound<TValue>(TValue)` have no overload
  accepting a context or `JsonTypeInfo` (confirmed via `dotnet-inspect`) and, per a real spike, already
  produced zero AOT warnings on their own — `ConfigureHttpJsonOptions` registering `AdminApiJsonContext.Default`
  into `TypeInfoResolverChain` is correct and sufficient for those two. But the 6 `Results.Json(...)` calls
  kept warning even after that registration: **the trim/AOT analyzer flags a call site based on which
  overload the C# compiler statically bound to, not on what `JsonOptions` DI resolves at runtime** —
  `Results.Json(value)` alone always binds to the `(TValue, JsonSerializerOptions?, ...)` overload regardless
  of DI wiring. Fixed by passing `AdminApiJsonContext.Default` explicitly as the second argument to each of
  the 6 calls, binding them to the `(TValue, JsonSerializerContext, ...)` overload instead.
- **`AdminApiJsonContext` needs explicit camelCase naming — a second real bug the integration tests caught.**
  Passing a context directly to `Results.Json(value, context)` makes that call use the **context's own**
  `JsonSerializerOptions`, not the ambient DI-configured ones — and a source-generated context defaults to
  PascalCase unless told otherwise. `AdminApiIntegrationTests.RoutesWithKeyAreSanitized`/
  `VersionReturnsBuildVersion` failed for real against the first attempt (`"RequiresAuth"` instead of the
  expected `"requiresAuth"`) before `[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]`
  was added to match minimal APIs' own default. This is the actual mechanism that keeps the proposal's "zero
  observable response-shape change" promise true — not the anonymous-to-named-record swap alone.
- **Anonymous objects → named records, required not optional.** `[JsonSerializable(typeof(...))]` cannot
  reference an anonymous type. `new { error = "..." }` → `AdminApiModels.ErrorView(string Error)` (positional
  record); `new { host, path, matched = false }` → `AdminApiModels.ResolveNotFoundView` as an **init-property**
  record, not positional — a positional `bool Matched` parameter tripped `AV1564`/CSharpGuidelinesAnalyzer
  ("a bool parameter is often meaningless at the call site"), matching this same file's own existing mixed
  style (`SecurityView` already uses init-properties for the same reason).
- **`StaticConfigProvider.cs`: the `JsonTypeInfo<TValue>`-accepting `Deserialize` overload, not
  `TypeInfoResolver` on the ambient options — same overload-resolution lesson as `AdminEndpoints.cs`.** The
  original plan (attach `TypeInfoResolver = StaticConfigJsonContext.Default` to the existing
  `SerializerOptions` field) compiled but, per the same real spike, left the warning in place — `Deserialize<T>(json,
  JsonSerializerOptions)` is still the bound overload regardless of what resolver that options instance
  carries. Fixed: `JsonSerializer.Deserialize(json, StaticConfigJsonContext.Default.StaticConfigFile)` (the
  `JsonTypeInfo<StaticConfigFile>` overload). `PropertyNameCaseInsensitive = true` moved onto the context via
  `[JsonSourceGenerationOptions(...)]` — deserialization-only, no naming-policy mismatch risk the way
  `AdminEndpoints.cs`'s serialization-side bug had, since case-insensitive matching doesn't depend on a
  naming policy. The now-dead `SerializerOptions` field was removed. `StaticConfigFile` and its nested
  `ClusterEntry`/`RouteEntry`/`OverrideEntry` types are `internal`; the context (same `DockYarp.App`
  assembly) sees them without any visibility change.
- **Two separate contexts, not one shared** — unchanged from the original plan. `AdminApiModels`' types live
  in `DockYarp.AdminApi`; `StaticConfigFile` is `internal` to `DockYarp.App`. `AdminApiJsonContext` had to
  become `public` (not `internal` as first written) since `DockYarp.App` — a different assembly — needs to
  reference it for both the `ConfigureHttpJsonOptions` registration and the per-call arguments; a compile
  error caught this immediately, cheap to fix.

## Risks / Trade-offs

- [Trade-off] `Results.BadRequest`/`NotFound`'s reliance on DI-resolved `JsonOptions` (rather than an
  explicit per-call context) means a future new `BadRequest`/`NotFound` call elsewhere in `DockYarp.App`
  serializing a type NOT covered by `AdminApiJsonContext` would silently fall back to reflection (a warning
  at AOT-publish time, not a runtime failure under JIT) rather than a compile error. Accepted — standard
  ASP.NET Core minimal-API AOT pattern, not DockYarp-specific, and the AOT spike is the acceptance gate that
  catches it when it matters.
- [Risk, materialized and fixed] The two "compiles clean but doesn't actually fix the AOT warning" mistakes
  above (DI-only registration for `Results.Json`, options-attached-resolver for `Deserialize`) are a real,
  general lesson worth remembering for any future `JsonSerializerContext` adoption in this codebase: **the
  trim/AOT analyzer only recognizes the overload actually selected by the C# compiler at the call site** —
  passing a `JsonSerializerContext`/`JsonTypeInfo<T>` argument directly, not merely making one reachable via
  DI/ambient options, is what the analyzer needs to see.

## Migration Plan

Single-PR swap, no phased rollout (internal serialization-mechanism change, zero external contract change —
JSON shapes are unchanged, verified by both the existing test suite passing unchanged and a real
`-p:PublishAot=true` spike). Rollback is a plain revert.
