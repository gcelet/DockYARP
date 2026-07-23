# Deployment (image, Compose, CI/CD)

## Docker image

A multi-stage [`Dockerfile`](../Dockerfile) builds on the .NET SDK and ships a minimal **chiseled**
runtime (`mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`), running as a **non-root** user. Chiseled
images carry no ICU, so the app sets `InvariantGlobalization=true` (it uses invariant/ordinal operations).

- Listens on **8080** (HTTP — ACME challenge + redirects) and **8443** (HTTPS — SNI per host, self-signed
  fallback for unknown hosts).
- Mounts **`/certs`** (certificate store) and **`/config`** (static configuration).

```bash
docker build -t dockyarp:local .
```

## Reference Compose stack

[`docker-compose.yml`](../docker-compose.yml) runs DockYarp in front of a labeled sample service
(`traefik/whoami`, `VIRTUAL_HOST=whoami.local`). DockYarp discovers containers via the Docker socket.

```bash
docker compose up -d --build
curl -H "Host: whoami.local" http://localhost/    # proxied to whoami
docker compose down -v
```

- **Docker discovery is opt-in**: enabled here via `Docker__Enabled=true`. It is off by default (so tests
  and local `dotnet run` need no daemon).
- The Docker socket is mounted **read-only** (`/var/run/docker.sock:ro`) — same model as nginx-proxy.
- **TLS**: the local demo uses HTTP + the self-signed fallback. Real ACME certificates need public DNS and
  the production ACME directory (see [tls-acme.md](tls-acme.md)).

## Graceful shutdown

On SIGTERM the host drains in-flight requests and stops background workers within
`Host:ShutdownTimeoutSeconds` (default 30s).

## Configuration

Options are bound from `appsettings.json` and environment variables (double-underscore syntax, e.g.
`Tls__AcceptTermsOfService=true`). Unset keys keep their defaults.

| Section | Keys |
|---|---|
| `Tls` | `ContactEmail`, `CertificateDirectory`, `AcmeDirectoryUri` (defaults to Let's Encrypt **staging**), `AcceptTermsOfService`, `RenewBeforeExpiry`, `CheckInterval` |
| `Security` | `EnableHsts`, `HstsMaxAge`, `HstsIncludeSubDomains`, `FrameOptions`, `ReferrerPolicy` |
| `Docker` | `Enabled`, `DockerEndpoint` |
| `AdminApi` | `ApiKey` (empty ⇒ admin API closed) |
| `Host` | `ShutdownTimeoutSeconds` |

For production TLS, set `Tls__AcmeDirectoryUri` to the production ACME endpoint, `Tls__AcceptTermsOfService=true`,
and `Tls__ContactEmail`.

## CI/CD with Nuke

[`build/Build.cs`](../build/Build.cs) defines the pipeline:

| Target | Action |
|---|---|
| `Restore` / `Compile` / `Test` | Restore, build, and test `DockYarp.slnx`. `Test` **excludes** the end-to-end suite (`TestCategory!=EndToEnd`), so the default flow needs no Docker. |
| `Publish` | Publish `DockYarp.App` to `artifacts/publish`. |
| `DockerImage` | `docker build` the image (depends on `Test`; the build stage runs the Nuke build). |
| `DockerPublish` | Build then `docker push` to the configured registry (depends on `DockerImage`). |
| `E2E` | Build the `dockyarp:local` and echo-backend images, then run the **Aspire** end-to-end suite (`TestCategory=EndToEnd`). Opt-in; never a dependency of the default flow. |
| `Smoke` | Bring up the reference Compose stack, probe the sample service by its `VIRTUAL_HOST`, then tear it down. Opt-in; never a dependency of the default flow. |
| `Release` | Validate a version through the full gate: depends on `Test`, `E2E`, and `DockerImage`. |

```bash
./build.sh Test          # or ./build.ps1 Test  — no Docker required
./build.sh DockerImage
./build.sh E2E           # requires a Docker daemon reachable by Aspire's DCP
./build.sh Smoke         # requires Docker (with the compose plugin) on PATH
./build.sh Release --version 1.2.3
```

### End-to-end tests (Aspire)

The `tests/DockYarp.E2E.*` projects boot a real distributed system with [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/):
DockYarp runs as a container mounting the Docker socket, in front of labeled `traefik/whoami` and a small
custom echo backend, and the NUnit harness asserts the proxy behaviour over HTTP. The suite is tagged
`[Category("EndToEnd")]` so it only runs under `E2E`/`Release`, never in the default `Test`.

> **Prerequisite**: a Docker daemon reachable by Aspire's orchestrator (DCP). When Docker runs in WSL, point
> `DOCKER_HOST`/the Docker context at it before running `E2E`/`Release`.

### Publishing to a registry

`DockerPublish` builds the image (the Docker build stage runs the Nuke build) and pushes it. The image
reference is `{Registry}/{ImageRepository}:{ImageTag}`, or `{ImageRepository}:{ImageTag}` on Docker Hub.

| Parameter | Default | Meaning |
|---|---|---|
| `--registry` | *(empty)* | Registry host; empty targets Docker Hub. |
| `--image-repository` | `dockyarp` | Image repository name. |
| `--image-tag` | `latest` | Image tag. |

Publishing **assumes you are already authenticated** (`docker login` beforehand); the build handles no
credentials.

```bash
docker login <registry>                 # once, outside the build
./build.sh DockerPublish                                   # -> dockyarp:latest on Docker Hub
./build.sh DockerPublish --registry registry.example.com --image-repository team/dockyarp --image-tag 1.2.3
```

The `Smoke` target runs the Compose smoke test (`docker compose up` + a single `VIRTUAL_HOST` probe, then
teardown) — a lightweight check independent of the Aspire `E2E` suite described above.
