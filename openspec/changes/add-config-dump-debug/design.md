# Design — add-config-dump-debug

## Context
The admin API (`AdminEndpoints`) exposes `/api/{routes,clusters,certs,health}` behind
`ApiKeyEndpointFilter`, with `AdminMapper` producing sanitized views (no secrets). Runtime route resolution is
`RouteMatcher.TryMatch(host, path)` (built from the snapshot's routes + the default host) — the same resolution
`RouteLookup` uses for security/TLS decisions.

## Decisions

### 1. A single "explain" endpoint, admin-only
Add `GET /api/resolve?host=&path=` in the existing `/api` group (so it inherits the API-key filter). It is the
DockYarp analog of `DEBUG_ENDPOINT`, but consolidated and never a public per-vhost endpoint. Missing `host` →
400; no match → 404 with `matched: false`.

### 2. Match exactly like runtime
The endpoint builds `new RouteMatcher(snapshot.Routes, routing.DefaultHost)` and calls `TryMatch` — identical to
what `RouteLookup` does per request — so the dump reflects real behavior (including wildcard/regex/default-host
resolution). `AdminApi` already depends on `Core`, so `RouteMatcher`/`RoutingOptions` are directly usable.

### 3. Sanitized composite view
`ResolveView` composes the existing `RouteView` (host/path/priority/cluster/requiresAuth/tls) plus a
`TransformsView` (path rewrites + response headers) and a `SecurityView` (internal-only, client-certificate
requirement, max body size) and the resolved `ClusterView`. `AdminMapper.Resolve(route, cluster)` reuses the
existing `ToRoute`/`ToCluster` mappers. No secrets are exposed (auth is shown as a boolean, consistent with
`/api/routes`).

## Verification
- Unit: `AdminMapper.Resolve` mapping. Integration: `/api/resolve` authenticated returns the resolved
  route/cluster JSON; unauthenticated is 401 (the group filter). No e2e needed.

## Risks
- The endpoint rebuilds a `RouteMatcher` per call; acceptable for a rarely-hit admin/debug endpoint.
