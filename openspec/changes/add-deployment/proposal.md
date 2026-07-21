## Why

DockYarp must ship as a container people can run like nginx-proxy: a minimal image plus a reference
Compose stack demonstrating label-based configuration. This change packages the app, wires Docker
discovery into the host, adds graceful shutdown, and drives it all from Nuke.

## What Changes

- Add a **multi-stage Dockerfile** whose **build stage runs the Nuke pipeline via `build.sh`** and whose
  runtime stage is a minimal **chiseled** .NET image (non-root), exposing the proxy ports and supporting
  mounted `/certs` and `/config` volumes.
- Wire **Docker discovery** into the host (gated by `Docker:Enabled`, default off so tests are unaffected)
  so a deployed instance configures itself from container labels.
- Add **graceful shutdown**: a bounded shutdown timeout so in-flight requests drain and background workers
  stop cleanly on SIGTERM.
- Add a reference **`docker-compose.yml`** (DockYarp + a labeled sample service) demonstrating
  label-based routing, and an **E2E smoke test** driven by Nuke (requires Docker on PATH).
- Implement **Nuke** CI/CD targets (restore, compile, test, publish, image, E2E).

## Capabilities

### New Capabilities
- `deployment`: official chiseled Docker image, reference Compose stack, and graceful shutdown.

### Modified Capabilities
<!-- None. Docker discovery is wired in the host (no spec change); its capability is unchanged. -->

## Impact

- **Code**: `Dockerfile`, `.dockerignore`, `docker-compose.yml`, `build/Build.cs` (Nuke targets),
  `scripts/` (E2E), graceful-shutdown + gated discovery wiring in `DockYarp.App`;
  `InvariantGlobalization=true` so the chiseled image needs no ICU.
- **Testing**: a small config/shutdown test runs in `dotnet test`; the Compose E2E runs via `nuke E2E`
  and **requires Docker on PATH** (not run in the default suite / this environment).
- **Owning agent**: AG-DEP.
