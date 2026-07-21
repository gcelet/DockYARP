## 1. Host wiring (AG-DEP)

- [x] 1.1 Set `InvariantGlobalization=true` in `DockYarp.App`
- [x] 1.2 Add graceful shutdown (`HostOptions.ShutdownTimeout` from `Host:ShutdownTimeoutSeconds`)
- [x] 1.3 Wire Docker discovery gated by `Docker:Enabled` (default false); read endpoint from config

## 2. Docker image (AG-DEP)

- [x] 2.1 Write a multi-stage `Dockerfile`: build stage runs the Nuke build (`build.sh Publish`), runtime stage is chiseled aspnet (non-root, `/certs` `/config`, exposed ports)
- [x] 2.2 Add `.dockerignore` (keep sources + build scaffolding; exclude outputs/noise)

## 3. Reference Compose stack (AG-DEP)

- [x] 3.1 Write `docker-compose.yml`: DockYarp (socket mounted read-only, `Docker__Enabled=true`, volumes, ports) + a labeled sample service
- [x] 3.2 Sample service with `VIRTUAL_HOST`/`VIRTUAL_PORT` labels

## 4. Nuke CI/CD (AG-DEP)

- [x] 4.1 Implement Nuke targets: Clean, Restore, Compile, Test, Publish
- [x] 4.2 Implement `DockerImage` (docker build) and `E2E` (compose up → probe → down) targets

## 5. E2E script (AG-DEP)

- [x] 5.1 Add an E2E script (compose up, probe sample service by VIRTUAL_HOST, tear down) — requires Docker on PATH

## 6. Tests & docs (AG-DEP)

- [x] 6.1 Test: graceful shutdown timeout is configured; discovery is off when `Docker:Enabled` is false
- [x] 6.2 Document the image, Compose stack, socket mount, and Nuke targets in `docs/`
