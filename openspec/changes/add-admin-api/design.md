## Context

The admin API and metrics run on the proxy's ASP.NET host. Read models come from the `proxy-routing`
store. Explicit endpoints (`/api/*`, `/metrics`) take routing precedence over YARP's catch-all, so they are
served by the host while everything else is proxied. `DockYarp.AdminApi` already references `Core`.

## Goals / Non-Goals

**Goals:**
- Read-only JSON endpoints for routes, clusters, certs, health — sanitized (no secrets).
- API-key protection for `/api/*`.
- OpenTelemetry metrics exposed at `/metrics` in Prometheus format.

**Non-Goals:**
- No write/admin operations. No certificate data yet (`/api/certs` returns empty until `tls-acme`).
- No IP-allowlist protection (future). `/metrics` is left unauthenticated for scrapers.

## Decisions

- **Sanitized response DTOs** in `DockYarp.AdminApi` rather than serializing the model directly.
  Rationale: `RouteRule.Auth` carries a password; the DTO exposes only `RequiresAuth` (bool). DTOs also
  decouple the wire shape from internal types.
- **Minimal-API endpoints** grouped under `/api` with an **`IEndpointFilter`** (`ApiKeyEndpointFilter`)
  that checks the `X-Api-Key` header against `AdminApiOptions.ApiKey` using a fixed-time comparison; 401 on
  mismatch. Rationale: keeps auth local to the group; no global middleware.
- **OpenTelemetry metrics**: a singleton `DockYarpMetrics` owns a `Meter("DockYarp")` with
  `ObservableGauge`s (routes, clusters, endpoints) whose callbacks read `store.Current`. The host wires
  `AddOpenTelemetry().WithMetrics(m => m.AddMeter("DockYarp").AddPrometheusExporter())` and
  `MapPrometheusScrapingEndpoint()` (`/metrics`). Rationale: standard, extensible; the Meter is created
  eagerly at startup so gauges are present for the first scrape.
- **Endpoint precedence**: explicit routes win over YARP's catch-all, so admin/metrics paths are never
  proxied.

## Risks / Trade-offs

- Admin endpoints exist on every host (path-based). [Risk: a proxied backend also using `/api/...` is
  shadowed] → acceptable for now; a dedicated admin host/port is a future option. Documented.
- Prometheus AspNetCore exporter is a beta package → pinned explicitly in CPM.

## Migration Plan

Additive. Adds OpenTelemetry packages to the host and an ASP.NET framework reference to `DockYarp.AdminApi`.

## Open Questions

- Whether `/metrics` should also be protected — left open (scraper-friendly) for now.
