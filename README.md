<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="assets/wordmark-dark.svg">
  <img src="assets/wordmark.svg" alt="DockYARP" width="360">
</picture>

**A dynamic reverse proxy for Docker containers, built on YARP and .NET**

Automatic service discovery from container labels, dynamic routing, automatic TLS (ACME),
security middleware, and an admin API — an `nginx-proxy`-style experience, in modern .NET.

[![Documentation](https://img.shields.io/badge/docs-gcelet.github.io-2A7AE2)](https://gcelet.github.io/DockYARP/)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![Reverse proxy](https://img.shields.io/badge/proxy-YARP-0078D4)
![Build](https://img.shields.io/badge/build-Nuke-yellow)
![Spec-driven](https://img.shields.io/badge/spec--driven-OpenSpec-6E56CF)
![License](https://img.shields.io/badge/license-MIT-green)

</div>

> [!WARNING]
> **This project is built 100% by AI.** Every line of code, spec, and document in DockYarp was produced
> by an AI agent. It is **highly experimental**, provided **as-is with no guarantees**, and is **not intended
> for production** or any environment that matters to your business. Treat it as a learning/demonstration
> project — do not run it where an outage, a security flaw, or data loss would have real consequences.

---

## Overview

DockYarp watches your Docker containers and turns their labels into live reverse-proxy configuration —
no restarts, no hand-written routes. Start a container with a `VIRTUAL_HOST` label and it is instantly
routable; request a certificate with `LETSENCRYPT_HOST` and HTTPS is provisioned automatically.

## Features

- 🐳 **Docker auto-discovery** — routes/clusters from container labels (nginx-proxy compatible): multi-host,
  multi-port (`VIRTUAL_HOST_MULTIPORTS`), backend scheme (`VIRTUAL_PROTO`), path rewrite (`VIRTUAL_DEST`),
  priority, and **health-aware** + **network-aware** selection.
- 🔀 **Dynamic routing on YARP** — host/path matching, clusters, load balancing, health checks, a default
  (catch-all) host, per-cluster request timeout and per-route body-size limit; reloaded live.
- 🧾 **Configuration sources** — Docker discovery **and** a static JSON file, merged with precedence
  (static wins); works with or without Docker.
- 🔐 **TLS & certificates** — automatic ACME (acquire/renew), **operator-provided** PEM/PFX certs with
  **wildcard-parent** SNI selection, `HTTPS_METHOD` (redirect/noredirect/nohttp/nohttps) gated on real cert
  availability, TLS hardening (min version, ciphers, protocols), and **mutual TLS** (client-cert auth).
- 🛡️ **Security middleware** — HTTPS enforcement, Basic Auth (from labels), per-host **HSTS** (+preload),
  client-certificate enforcement, and hardening headers.
- 📊 **Admin API & observability** — read-only `/api/*` endpoints, Prometheus `/metrics`, and structured
  per-request **access logging**.
- 🧯 **Custom error pages** & **proxy tuning** — configurable error pages and request limits/timeouts.
- 📦 **Container-native** — minimal, non-root **chiseled** image; reference Docker Compose stack.

## Architecture

```mermaid
flowchart LR
  Containers["Docker containers<br/>(VIRTUAL_HOST, ... labels)"] -->|discovery| Store[("Routing store<br/>DockYarp.Core")]
  Static["Static configuration"] --> Store
  Store -->|live reload| YARP["YARP reverse proxy"]
  Client["Client"] --> Security["Security<br/>HTTPS · Basic Auth · headers"]
  Security --> YARP --> Backends[("Backend containers")]
  TLS["TLS / ACME<br/>certs · SNI"] -. serves .- YARP
  Admin["Admin API · /metrics"] -. reads .- Store
```

Each concern is a focused project; `DockYarp.Core` is a dependency-free leaf that everything else builds on.
See [`docs/`](docs/) for the details of each capability.

## Quick start

Run the reference stack (DockYarp + a labeled sample service):

```bash
docker compose up -d --build
curl -H "Host: whoami.local" http://localhost/    # proxied to the sample service
docker compose down -v
```

Expose your own service by adding labels:

```yaml
services:
  web:
    image: my/web
    labels:
      VIRTUAL_HOST: app.local
      VIRTUAL_PORT: "8080"
      LETSENCRYPT_HOST: app.local
      LETSENCRYPT_EMAIL: admin@example.com
```

DockYarp needs read-only access to the Docker socket (`/var/run/docker.sock`) to discover containers.

## Container labels

| Label | Description |
|---|---|
| `VIRTUAL_HOST` | Host(s) the container is exposed on — comma-separated for several (**required**\*). |
| `VIRTUAL_PORT` | Target port (inferred when a single port is exposed). |
| `VIRTUAL_PATH` / `VIRTUAL_DEST` | Path prefix matched, and destination rewrite (strip the prefix). |
| `VIRTUAL_PROTO` | Backend scheme: `http` (default) or `https`. |
| `VIRTUAL_HOST_MULTIPORTS` | \*YAML `host → path → { port, proto, dest }`; supersedes `VIRTUAL_HOST`/`VIRTUAL_PORT`. |
| `LETSENCRYPT_HOST` / `LETSENCRYPT_EMAIL` | Request an ACME certificate for the host. |
| `HTTPS_METHOD` | `redirect` (default), `noredirect`, `nohttp`, `nohttps`. |
| `HSTS` | Per-host `Strict-Transport-Security` value, or `off`. |
| `DOCKYARP_LB` / `DOCKYARP_PRIORITY` | Load-balancing policy and route priority. |
| `DOCKYARP_AUTH_USER` / `DOCKYARP_AUTH_PASSWORD` / `DOCKYARP_AUTH_REALM` | Basic Auth credentials. |
| `DOCKYARP_CLIENT_CERT` | Mutual-TLS requirement: `required`, `optional`, `none`. |
| `DOCKYARP_PROXY_TIMEOUT` / `DOCKYARP_MAX_BODY_SIZE` | Per-cluster request timeout and per-route body-size limit. |

Full reference: [`docs/labels-reference.md`](docs/labels-reference.md).

## Admin API & metrics

Protected by an `X-Api-Key` header (`AdminApi:ApiKey`):

| Endpoint | Description |
|---|---|
| `GET /api/routes` | Active routes (sanitized). |
| `GET /api/clusters` | Active clusters and endpoints. |
| `GET /api/certs` | Stored certificates (host + expiry, no private keys). |
| `GET /api/health` | Overall status with route/cluster/certificate counts and discovery status. |
| `GET /metrics` | Prometheus metrics (unauthenticated). |

Every request is also written to a structured **access log** (`AccessLog:Enabled`).
See [`docs/admin-api.md`](docs/admin-api.md).

## Build & test

The build is driven by [Nuke](https://nuke.build); `build.ps1` / `build.sh` bootstrap it anywhere .NET is installed.

```bash
./build.ps1 Test          # restore, build, and test (Windows)
./build.sh  Test          # Linux/macOS
```

Or directly with the .NET SDK (.NET 10, pinned by `global.json`):

```bash
dotnet build DockYarp.slnx
dotnet test  DockYarp.slnx
```

### Container image & publishing

```bash
./build.ps1 DockerImage                       # build the chiseled image
docker login <registry>                       # authenticate first
./build.ps1 DockerPublish --registry registry.example.com --image-repository team/dockyarp --image-tag 1.2.3
```

See [`docs/deployment.md`](docs/deployment.md).

## Project structure

```
src/
  DockYarp.Core/      # models, interfaces, stores (leaf)
  DockYarp.Docker/    # Docker discovery + label mapping
  DockYarp.Tls/       # ACME + certificates
  DockYarp.Security/  # HTTPS enforcement, auth, headers
  DockYarp.AdminApi/  # admin/observability endpoints
  DockYarp.App/       # ASP.NET host: YARP, DI, pipeline
tests/                   # one *.Tests project per src project (NUnit)
build/                   # Nuke build project
docs/                    # architecture & capability documentation
openspec/                # spec-driven development (specs + changes)
```

## Documentation

📖 **The full documentation site is published at [gcelet.github.io/DockYARP](https://gcelet.github.io/DockYARP/).**
The in-repo capability docs below go deeper on each concern:

| Topic | Document |
|---|---|
| **Architecture & nginx-proxy parity** | [architecture.md](docs/architecture.md) |
| Routing model | [routing-model.md](docs/routing-model.md) |
| Docker discovery | [docker-discovery.md](docs/docker-discovery.md) |
| YARP integration | [yarp-integration.md](docs/yarp-integration.md) |
| Security middleware | [security-middleware.md](docs/security-middleware.md) |
| TLS / ACME | [tls-acme.md](docs/tls-acme.md) |
| Admin API & observability | [admin-api.md](docs/admin-api.md) |
| Deployment | [deployment.md](docs/deployment.md) |

## Development

DockYarp is developed **spec-first** with [OpenSpec](https://github.com/Fission-AI/OpenSpec): every change
starts as a proposal under [`openspec/`](openspec/) and its specs are archived once implemented. Coding
conventions (modern .NET, strict analyzers, Central Package Management) live in [`AGENTS.md`](AGENTS.md),
the source of truth for both humans and AI assistants.

## License

Licensed under the MIT License — see [`LICENSE`](LICENSE).
