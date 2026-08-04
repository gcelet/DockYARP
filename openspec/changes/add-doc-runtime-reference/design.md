# Design — add-doc-runtime-reference (Runtime features page + light audit)

## Scope
Deliverable: one **Runtime features** page (`features.md`, weight after Configuration) documenting the runtime
behaviors at a user-facing altitude — behavior + a short example, not internals. Plus a light spot-check of the
existing pages for obvious staleness and cross-links. Worked recipes stay in `add-doc-examples`.

## Source of truth (verified for accuracy)
- **Admin API** (`AdminEndpoints`, behind the `X-Api-Key` filter): `GET /api/routes`, `/api/clusters`,
  `/api/certs`, `/api/health`, `/api/resolve?host=&path=` (404 on no match, 401 without a valid key). Key =
  `AdminApi:ApiKey` (empty ⇒ admin API closed).
- **Observability**: `/metrics` (Prometheus). Access-log field catalog (`AccessLogFields.Names`): `Method`,
  `Scheme`, `Host`, `Path`, `Query`, `Protocol`, `RemoteIp`, `UserAgent`, `Referer`, `StatusCode`, `ElapsedMs`.
  `AccessLog:Enabled` (true), `AccessLog:ExcludedPathPrefixes` (`/metrics`,`/api`), `AccessLog:Fields`.
- **Static config**: `StaticConfig:Path` → a JSON file with `Routes`/`Clusters`, merged with discovery (static
  wins); the sole source when `Docker:Enabled=false`.
- **Error pages**: `ErrorPages:Directory` → `{statusCode}.html` overlaid on DockYARP-generated error responses.
- **Discovery / routing / TLS / LB**: from the archived `openspec/specs/` (docker-discovery, proxy-routing,
  yarp-dynamic-config, tls-acme) — health-aware, network selection, filters; exact-beats-wildcard, priority,
  multiport, `VIRTUAL_DEST`; the five LB policies; ACME + SNI + mTLS.
- **Shutdown**: `Host:ShutdownTimeoutSeconds` (30) drains in-flight requests.

## Non-goals
Exhaustive internals, and a full page-by-page rewrite of getting-started/architecture/deployment (only obvious
fixes + cross-links here).
