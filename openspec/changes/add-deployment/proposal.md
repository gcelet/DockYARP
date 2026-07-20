## Why

DockYarp must ship as a container that people can run like nginx-proxy: a minimal image plus a
reference Compose stack demonstrating label-based config and automatic TLS. Clean shutdown avoids
dropping in-flight requests on redeploys.

> Status: **sketch** — proposal + spec intent only. Design and tasks to be detailed just-in-time when
> this phase starts.

## What Changes

- Provide a minimal, multi-stage Docker image supporting mounted `/certs` and `/config` volumes.
- Provide a reference `docker-compose.yml` (DockYarp + ACME + sample services) with TLS.
- Implement graceful shutdown (drain in-flight requests, stop background workers cleanly) on container stop.

## Capabilities

### New Capabilities
- `deployment`: official Docker image, reference Compose stack, and graceful shutdown.

### Modified Capabilities
<!-- None. -->

## Impact

- **Code**: `Dockerfile`, `docker-compose.yml`, host lifecycle wiring in `DockYarp.App`; E2E scripts.
- **Upstream**: exercises all prior capabilities end-to-end. **Owning agent**: AG-DEP.
