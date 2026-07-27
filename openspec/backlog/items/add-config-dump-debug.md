---
id: add-config-dump-debug
capability: admin-api
agent: AG-AA
tier: C-doc
priority: low
status: backlog
nginx-proxy: DEBUG_ENDPOINT (/nginx-proxy-debug)
provenance: this parity pass (matrix: mostly covered by admin API)
---

## Why
nginx-proxy's `DEBUG_ENDPOINT` returns the effective global + vhost + path config as JSON for
troubleshooting. DockYarp's admin API already exposes routes/clusters/certs/health, so the gap is small — a
consolidated "effective config for this host/path" view.

## nginx-proxy behavior
- `DEBUG_ENDPOINT` (per proxy or per container, default off) serves `<host>/nginx-proxy-debug` returning the
  resolved global/vhost/path configuration as JSON (`test_debug-endpoint`).

## DockYarp today
Admin API exposes `/api/routes`, `/api/clusters`, `/api/certs`, `/api/health`
(`src/DockYarp.AdminApi/AdminEndpoints.cs`), API-key protected. There is no single "explain this host/path"
resolution dump.

## Proposed change (sketch)
Add an admin endpoint (e.g. `GET /api/resolve?host=&path=`) returning the matched route, cluster, transforms,
TLS metadata, and applied security policy for a given host/path — the DockYarp analog of `DEBUG_ENDPOINT`.
Keep it behind the admin API key (never a public per-vhost endpoint).

## Acceptance criteria (→ scenarios)
- **WHEN** an authenticated operator queries resolve for a host+path **THEN** the effective route/cluster/
  transforms/TLS/security are returned as JSON.
- **WHEN** the request is unauthenticated **THEN** it is rejected (401), like other admin endpoints.

## Notes / risks / references
- Reuse the existing single-route resolution (`share-route-resolution`) so the dump matches runtime behavior.
