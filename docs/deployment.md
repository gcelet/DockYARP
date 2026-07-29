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
(`traefik/whoami`, `VIRTUAL_HOST=whoami.local`).

```bash
docker compose up -d --build
curl -H "Host: whoami.local" http://localhost/    # proxied to whoami
docker compose down -v
```

- **Docker discovery is opt-in**: enabled here via `Docker__Enabled=true`. It is off by default (so tests
  and local `dotnet run` need no daemon).
- **DockYarp runs as a non-root container** and must not run as root, so it does **not** read the Docker
  socket directly (it is owned `root:docker 660`). See *Docker API access* below.

### Docker API access (non-root)

DockYarp needs the Docker API but runs unprivileged. Two supported modes:

**Socket proxy (recommended, the default in `docker-compose.yml`).** A minimal
[`tecnativa/docker-socket-proxy`](https://github.com/Tecnativa/docker-socket-proxy) container mounts the
socket and exposes a **read-only** Docker API over TCP; DockYarp points at it and mounts no socket:

```yaml
dockerproxy:
  image: tecnativa/docker-socket-proxy
  environment: { CONTAINERS: "1" }        # + the image's default event stream
  volumes: [ "/var/run/docker.sock:/var/run/docker.sock:ro" ]
dockyarp:
  environment:
    Docker__DockerEndpoint: "tcp://dockerproxy:2375"
```

**Group membership (alternative).** [`examples/docker-compose.group-add.yml`](../examples/docker-compose.group-add.yml)
keeps a direct socket mount and adds the container to the socket's owning group. Provide the host GID first:

```bash
export DOCKER_GID=$(stat -c '%g' /var/run/docker.sock)
docker compose -f examples/docker-compose.group-add.yml up -d --build
```
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
| `Security` | `EnableHsts`, `HstsMaxAge`, `HstsIncludeSubDomains`, `FrameOptions`, `ReferrerPolicy`, `ServerHeader`, `InternalRanges` (CIDRs for `NETWORK_ACCESS=internal`), `HtpasswdDirectory` (file-based Basic Auth), `HtpasswdReloadInterval` (default 30s) |
| `Docker` | `Enabled`, `DockerEndpoint` |
| `AdminApi` | `ApiKey` (empty ⇒ admin API closed) |
| `Host` | `ShutdownTimeoutSeconds` |
| `DataProtection` | `CertificatePath`, `CertificatePassword` (optional PFX to encrypt the key ring at rest) |
| `Compression` | `Enabled` (gzip/brotli response compression; default `true`) |

For production TLS, set `Tls__AcmeDirectoryUri` to the production ACME endpoint, `Tls__AcceptTermsOfService=true`,
and `Tls__ContactEmail`.

DockYarp runs non-root and persists its state — ACME certificates and (transitively-registered, via YARP) Data
Protection keys — to the mounted **`/certs`** volume (`Tls__CertificateDirectory=/certs`, set in the image).
The image creates `/certs` **owned by the app user** so the non-root process can write it; the reference
Compose stack therefore uses a **named volume** (which inherits that ownership) rather than a host bind mount
— a bind mount would re-impose the host directory's ownership and break the non-root write. Data Protection
keys are stored under `/certs/dataprotection-keys`, so any protected data survives container recreation.

**At-rest encryption of Data Protection keys is opt-in.** Data Protection currently protects **no sensitive
payload** in DockYarp (no session affinity is configured, and there are no cookies, antiforgery, or auth-cookie),
so by default the key ring is persisted unencrypted and the benign "keys may be persisted unencrypted" warning is
suppressed. To encrypt the key ring at rest, set `DataProtection__CertificatePath` (and
`DataProtection__CertificatePassword` if the PFX is protected) to an X.509 certificate — DockYarp uses ASP.NET's
built-in certificate encryptor. **Store that certificate outside the `/certs` volume**: a certificate sitting
next to the keys it protects provides no real at-rest protection. A misconfigured certificate (missing file or
wrong password) fails startup rather than silently falling back to unencrypted keys. When a feature that actually
depends on Data Protection (e.g. session affinity) is added, it will *require* this certificate and fail fast if
it is absent.

## CI/CD with Nuke

[`build/Build.cs`](../build/Build.cs) defines the pipeline:

| Target | Action |
|---|---|
| `Restore` / `Compile` / `Test` | Restore, build, and test `DockYarp.slnx`. `Test` runs the unit/integration test projects and **excludes** the end-to-end project (by project, so it needs no Docker and runs deterministically). |
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
custom echo backend, and the NUnit harness asserts the proxy behaviour over HTTP. It also runs a
`smallstep/step-ca` ACME server to assert TLS end to end (real certificate provisioning over HTTP-01, the
self-signed fallback, HTTP→HTTPS redirect, per-host HSTS, and mutual TLS). The suite is tagged
`[Category("EndToEnd")]` so it only runs under `E2E`/`Release`, never in the default `Test`.

> **Prerequisite**: a Docker daemon reachable by Aspire's orchestrator (DCP). When Docker runs in WSL, point
> `DOCKER_HOST`/the Docker context at it before running `E2E`/`Release`.

**Diagnostics.** The containers are torn down at the end of a run, so the suite streams **each Aspire
resource's logs to `artifacts/e2e-logs/<resource>.log`** during the run (e.g. `dockyarp.log`, `stepca.log`).
The directory is recreated at the start of each `E2E` run (only the last run is kept) and is git-ignored. On
failure the `E2E` target prints the directory and a tail of `dockyarp.log`. Running the suite directly with
`dotnet test` writes the logs next to the test assembly unless `DOCKYARP_E2E_LOG_DIR` is set.

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
