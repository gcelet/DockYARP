## 1. Model + applier (AG-RP)
- [x] 1.1 `RouteTransforms`: add `ResponseHeaders` (name → value)
- [x] 1.2 New `ConfigOverrides` (PerHost + Default) with an `Empty` value
- [x] 1.3 New pure `RouteOverrideApplier.Apply(routes, overrides)`: host-specific headers else default, merged
      into each route's `Transforms.ResponseHeaders`
- [x] 1.4 `IStaticConfigProvider.GetOverrides()` as a default interface method returning `ConfigOverrides.Empty`

## 2. Static config + mapping + wiring (AG-RP)
- [x] 2.1 `StaticConfigFile`/`StaticConfigProvider`: parse an `overrides` array (`host` incl. `default`,
      `responseHeaders`) into `ConfigOverrides`; implement `GetOverrides()`
- [x] 2.2 `YarpConfigMapper.BuildTransforms`: emit `{ ResponseHeader, Set, When=Always }` for each response header
- [x] 2.3 Apply overrides after `Merge` in `StaticConfigService` and `DiscoveryReconciler`

## 3. Tests (AG-RP)
- [x] 3.1 `RouteOverrideApplier`: per-host headers; default fallback; host-specific wins; merge with an existing
      (path) transform; empty overrides leave routes unchanged
- [x] 3.2 `YarpConfigMapper`: a route with response headers emits the YARP `ResponseHeader` transforms
- [x] 3.3 `StaticConfigProvider`: an `overrides` section is parsed (per-host + `default`)
- [x] 3.4 Route replacement: covered by `RouteConfigMergerTests.ConflictingHostResolvedByPrecedence`
      (static replaces discovered for the same host/path)

## 4. Verify (AG-RP)
- [x] 4.1 Nuke `Test` gate green
