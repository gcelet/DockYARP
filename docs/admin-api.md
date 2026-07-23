# Admin API & observability (DockYarp.AdminApi)

DockYarp exposes a protected, read-only admin API and Prometheus metrics on the proxy host. Explicit
endpoints take routing precedence over YARP's catch-all, so `/api/*` and `/metrics` are served by the host
and everything else is proxied.

## Endpoints

| Endpoint | Returns |
|---|---|
| `GET /api/routes` | Active routes (sanitized — see below). |
| `GET /api/clusters` | Active clusters with their endpoints. |
| `GET /api/certs` | Stored certificates (host + expiry, no private keys) from the certificate store. |
| `GET /api/health` | Overall status (`Healthy`/`Degraded`) with route/cluster/certificate counts and Docker discovery status (`connected`/`disconnected`/`disabled`). |
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

## Access logging

`AccessLogMiddleware` runs first in the pipeline and emits a **structured** access-log entry per request
(method, scheme, host, path, response status, elapsed ms) via source-generated logging — covering proxied
requests, redirects, and unmatched responses. Disable it with `AccessLog:Enabled=false`. The rendered format
follows the configured logging provider: enable JSON with the console logger, e.g.

```jsonc
// appsettings.json
{ "Logging": { "Console": { "FormatterName": "json" } } }
```

## Note

Admin endpoints are path-based and currently exist on every host; a dedicated admin host or port is a
future option.
