## 1. Models + mapper (AG-AA)
- [x] 1.1 `AdminApiModels`: add `TransformsView` (path rewrites + response headers), `SecurityView`
      (internal-only, client-cert requirement, max body size), and `ResolveView`
- [x] 1.2 `AdminMapper.Resolve(route, cluster)`: compose the views (reuse `ToRoute`/`ToCluster`), no secrets

## 2. Endpoint (AG-AA)
- [x] 2.1 `AdminEndpoints`: `GET /api/resolve?host=&path=` in the API-key group — build `RouteMatcher(snapshot,
      defaultHost)`, `TryMatch`; 400 on missing host, 404 on no match, else the `ResolveView` JSON

## 3. Tests (AG-AA)
- [x] 3.1 `AdminMapper.Resolve`: maps route/transforms/security/cluster into the view; tolerates a missing cluster
- [x] 3.2 Integration: `/api/resolve` with a key returns the resolved route/cluster; unknown host is 404;
      without a key is 401

## 4. Verify (AG-AA)
- [x] 4.1 Nuke `Test` gate green
