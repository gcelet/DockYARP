# Admin API & observability (DockYarp.AdminApi)

DockYarp exposes a protected, read-only admin API and Prometheus metrics on the proxy host. Explicit
endpoints take routing precedence over YARP's catch-all, so `/api/*` and `/metrics` are served by the host
and everything else is proxied.

## Endpoints

| Endpoint | Returns |
|---|---|
| `GET /api/routes` | Active routes (sanitized — see below). |
| `GET /api/clusters` | Active clusters with their endpoints. |
| `GET /api/certs` | Empty list for now (a certificate store arrives with `tls-acme`). |
| `GET /api/health` | Overall status plus route/cluster counts. |
| `GET /metrics` | OpenTelemetry metrics in Prometheus format (unauthenticated, for scrapers). |

Responses are **sanitized DTOs**: a route exposes `requiresAuth` (bool) but never the Basic Auth password.

## Protection

`/api/*` is guarded by an `X-Api-Key` header compared (fixed-time) against `AdminApi:ApiKey`. A missing or
invalid key yields 401. If no key is configured, the admin API is closed (all 401). `/metrics` is left
unauthenticated so Prometheus can scrape it.

```jsonc
// appsettings.json
{ "AdminApi": { "ApiKey": "<your-key>" } }
```

## Metrics

`DockYarpMetrics` owns a `Meter("DockYarp")` with observable gauges read from the routing store:

| Instrument | Meaning |
|---|---|
| `dockyarp.routes` | Active route count. |
| `dockyarp.clusters` | Active cluster count. |
| `dockyarp.endpoints` | Total endpoints across clusters. |

The host wires `AddOpenTelemetry().WithMetrics(m => m.AddMeter("DockYarp").AddPrometheusExporter())` and
`MapPrometheusScrapingEndpoint()`. The meter is created eagerly at startup so the gauges are present for the
first scrape.

## Note

Admin endpoints are path-based and currently exist on every host; a dedicated admin host or port is a
future option.
