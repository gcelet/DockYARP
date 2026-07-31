## Why
nginx-proxy's `DEBUG_ENDPOINT` returns the resolved config for troubleshooting. DockYarp's admin API already
exposes routes/clusters/certs/health, so the only gap is a consolidated **"explain this host/path"** view — the
effective route, transforms, TLS, security, and target cluster for a given host/path.

## What Changes
- Add `GET /api/resolve?host=&path=` to the admin API (behind the existing API-key filter, never a public
  per-vhost endpoint). It resolves the host/path with the **same route matcher used at runtime** and returns the
  matched route, its transforms, TLS metadata, security policy, and resolved cluster as sanitized JSON.
- Missing `host` → 400; no route match → 404 (matched: false); unauthenticated → 401 like the other admin
  endpoints.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `admin-api`: adds an authenticated configuration-resolution ("explain") endpoint.

## Impact
- **Code**: `DockYarp.AdminApi` — `AdminApiModels` (`TransformsView`, `SecurityView`, `ResolveView`),
  `AdminMapper.Resolve`, and the `/api/resolve` endpoint (uses `RouteMatcher` + `RoutingOptions`, matching
  runtime resolution).
- **Tests**: `AdminMapper.Resolve` mapping; integration `/api/resolve` (authenticated returns the resolved
  route/cluster; unauthenticated is 401).
- **Owning agent**: AG-AA. Resolves `add-config-dump-debug`.
