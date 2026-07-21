## 1. AdminApi project setup (AG-AA)

- [x] 1.1 Add `Microsoft.AspNetCore.App` FrameworkReference to `DockYarp.AdminApi`
- [x] 1.2 Add `AdminApiOptions` (API key)

## 2. Response DTOs & mapping (AG-AA)

- [x] 2.1 Define sanitized DTOs (route, cluster, endpoint, health) — expose `RequiresAuth`, never the password
- [x] 2.2 Map the current snapshot to DTOs

## 3. Endpoints & protection (AG-AA)

- [x] 3.1 Implement `ApiKeyEndpointFilter` (checks `X-Api-Key`, fixed-time compare, 401 on mismatch)
- [x] 3.2 Implement `MapAdminApi` mapping `/api/routes|clusters|certs|health` under the api-key filter
- [x] 3.3 `/api/certs` returns an empty list (no certificate store yet)

## 4. Metrics (AG-AA)

- [x] 4.1 Implement `DockYarpMetrics` (Meter "DockYarp" + ObservableGauges for routes/clusters/endpoints)

## 5. Host wiring (AG-AA)

- [x] 5.1 Add OpenTelemetry packages to CPM and reference them in `DockYarp.App`
- [x] 5.2 Wire `AddOpenTelemetry().WithMetrics(... AddMeter("DockYarp").AddPrometheusExporter())`
- [x] 5.3 Register admin services, `MapAdminApi()`, and `MapPrometheusScrapingEndpoint()` (`/metrics`); create the Meter eagerly

## 6. Tests (AG-AA)

- [x] 6.1 Integration: `/api/routes` without key → 401; with key → 200
- [x] 6.2 Integration: `/api/routes` for an auth-protected route exposes `requiresAuth` but no password
- [x] 6.3 Integration: `/api/health` returns status; `/metrics` is scrapable (200, Prometheus text)

## 7. Documentation (AG-AA)

- [x] 7.1 Document the admin API and observability (endpoints, auth, metrics) in `docs/`
