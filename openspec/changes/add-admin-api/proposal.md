## Why

Operators need to inspect DockYarp's live state (routes, clusters, certificates, health) and the
system needs to be observable in production. This exposes a protected read-only API plus metrics/logs.

> Status: **sketch** — proposal + spec intent only. Design and tasks to be detailed just-in-time when
> this phase starts.

## What Changes

- Add read-only admin endpoints: `/api/routes`, `/api/clusters`, `/api/certs`, `/api/health`.
- Protect the admin API (token or IP allowlist).
- Add observability: structured logging and a Prometheus-style `/metrics` endpoint.

## Capabilities

### New Capabilities
- `admin-api`: protected read-only admin endpoints plus observability (structured logs + `/metrics`).

### Modified Capabilities
<!-- None: reads state from proxy-routing and tls-acme. -->

## Impact

- **Code**: `src/DockYarp.AdminApi` controllers + wiring in `DockYarp.App`; tests in `DockYarp.AdminApi.Tests`.
- **Dependencies**: a metrics exporter (e.g. `OpenTelemetry`/`prometheus-net`) — decided at phase start.
- **Owning agent**: AG-AA.
