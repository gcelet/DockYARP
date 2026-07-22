## 1. Routing model & options (AG-RP)

- [x] 1.1 Add `RoutingOptions` (`DefaultHost`, `DefaultResponseStatusCode` = 404) to `DockYarp.Core.Configuration`
- [x] 1.2 `RouteMatcher` takes an optional `defaultHost`; `TryMatch` falls back to the default host's routes when nothing else matches

## 2. Wiring (AG-RP / AG-SEC)

- [x] 2.1 `RouteLookup` injects `RoutingOptions` and passes `DefaultHost` to the matcher
- [x] 2.2 `YarpConfigMapper.Map` takes an optional `defaultHost` and appends a lowest-precedence catch-all route to the default host's cluster
- [x] 2.3 `YarpConfigBridge` injects `RoutingOptions` and passes `DefaultHost` to the mapper
- [x] 2.4 `Program` binds the `Routing` section, registers `RoutingOptions`, and maps a fallback returning `DefaultResponseStatusCode`

## 3. Tests & docs (AG-RP)

- [x] 3.1 Matcher tests: default host serves unknown hosts; no default host → no match
- [x] 3.2 Mapper tests: default host → catch-all route (any host, default cluster); no default host → none
- [x] 3.3 Integration: unmatched request returns the configured status (503); default host proxies an unknown host to its backend
- [x] 3.4 Document default host / default response in `docs/routing-model.md`
- [x] 3.5 Build + full test suite green via the Nuke CLI
