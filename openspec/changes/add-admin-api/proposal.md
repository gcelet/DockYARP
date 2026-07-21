## Why

Operators need to inspect DockYarp's live state (routes, clusters, certificates, health) and the system
needs to be observable in production. This exposes a protected read-only API plus Prometheus metrics.

## What Changes

- Add read-only admin endpoints: `/api/routes`, `/api/clusters`, `/api/certs`, `/api/health`, returning
  **sanitized** JSON (no secrets — Basic Auth passwords are never exposed).
- Protect the admin API with an **API key** header (`X-Api-Key`), returning 401 otherwise.
- Add **observability**: structured logging and OpenTelemetry metrics exposed at `/metrics` in Prometheus
  format (active routes, clusters, endpoints).

## Capabilities

### New Capabilities
- `admin-api`: protected read-only admin endpoints plus observability (structured logs + `/metrics`).

### Modified Capabilities
<!-- None: reads the routing store; no model changes. -->

## Impact

- **Code**: `src/DockYarp.AdminApi` (endpoints, response DTOs, API-key filter, options, metrics); wiring
  and OpenTelemetry setup in `DockYarp.App`.
- **Dependencies**: `DockYarp.AdminApi` references the ASP.NET shared framework; `DockYarp.App` adds
  `OpenTelemetry.Extensions.Hosting` and `OpenTelemetry.Exporter.Prometheus.AspNetCore` (CPM).
- **Deferred**: `/api/certs` returns an empty list until `tls-acme` provides a certificate store; IP
  allowlist protection is a future option.
- **Owning agent**: AG-AA.
