# Design — add-vhost-config-overrides

## Context
Routes/clusters come from discovery (`ContainerMapper`) and static config (`StaticConfigProvider`), merged by
`RouteConfigMerger` (static wins over discovery on the same host/path key). `RouteRule.Transforms`
(`RouteTransforms`) currently carries only path rewrites, mapped to YARP transforms by
`YarpConfigMapper.BuildTransforms`. There is no per-host/global augmentation of the generated routes.

## Decisions

### 1. Structured overrides, not raw config
The nginx `vhost.d` analog is a **structured** per-host/global override. Start with **response-header
injection** (the most common `vhost.d` use) plus the already-supported route replacement. No nginx syntax is
interpreted.

### 2. `ConfigOverrides` + a pure applier, after the merge
Add `ConfigOverrides` (Core): `PerHost` (host → headers, ordinal-ignore-case) and `Default` (headers for hosts
with no specific entry). A pure `RouteOverrideApplier.Apply(routes, overrides)` resolves, for each route, the
host-specific headers else the default, and merges them into `Transforms.ResponseHeaders`. It runs **after**
`RouteConfigMerger.Merge`, so it augments routes from *any* source. Both callers (`StaticConfigService`,
`DiscoveryReconciler`) apply it with the overrides from the static provider.

### 3. Overrides come from the static provider
`IStaticConfigProvider` gains `GetOverrides()` with a **default interface method** returning
`ConfigOverrides.Empty`, so existing implementers (`EmptyStaticConfigProvider`, test fakes) are unaffected.
`StaticConfigProvider` parses an `overrides` array from the JSON (`host` — a concrete host or `default` for the
global bucket — plus `responseHeaders`).

### 4. YARP-native response headers
`RouteTransforms.ResponseHeaders` (name → value) map to YARP config transforms
`{ "ResponseHeader": name, "Set": value, "When": "Always" }` (verified against the YARP transform docs):
`Set` replaces, `When=Always` applies on success and failure so a host always carries its headers.

### 5. Replace-generated-route is existing behavior
A static route with the same host/path replaces the discovered one through `RouteConfigMerger`'s source
precedence (a merge diagnostic already reports the conflict). This change documents/tests it rather than adding
new machinery.

## Verification
- Unit only: `RouteOverrideApplier` (Core.Tests), `YarpConfigMapper` response-header transform
  (IntegrationTests), `StaticConfigProvider` override parsing (IntegrationTests), and route replacement via the
  merger. No e2e needed — the mapping is deterministic and the transform shape is verified against YARP docs.

## Risks
- Sprawl: bounded here to response headers + replace-route; further override kinds are separate, structured
  additions. `default` is a reserved host name for the global bucket (documented), matching nginx.
