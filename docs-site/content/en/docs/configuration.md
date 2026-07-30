---
title: Configuration
weight: 2
description: Container labels and application configuration.
---

DockYARP is configured two ways: **container labels** (per backend, nginx-proxy compatible) and **application
configuration** (the proxy's own settings).

## Container labels

### Routing

| Label | Purpose |
|-------|---------|
| `VIRTUAL_HOST` | Host(s) to route to the container (comma-separated for several). |
| `VIRTUAL_PORT` | Target container port (required when the container exposes several ports). |
| `VIRTUAL_PATH` | Optional path prefix for the route. |
| `VIRTUAL_PROTO` | Backend protocol: `http` (default), `https`, `grpc`, `grpcs`. |
| `VIRTUAL_DEST` | Rewrite the matched path before forwarding. |
| `VIRTUAL_HOST_MULTIPORTS` | Map several host/path → port/proto entries on one container. |

### TLS

| Label | Purpose |
|-------|---------|
| `LETSENCRYPT_HOST` | Host to provision an ACME certificate for. |
| `LETSENCRYPT_EMAIL` | Contact email for the ACME account. |
| `HTTPS_METHOD` | `redirect` (default), `noredirect`, `nohttp`, `nohttps`. |
| `HSTS` | Per-host `Strict-Transport-Security` value, or `off`. |
| `CERT_NAME` | Pin the host to a named shared (SAN/wildcard) certificate. |

### Access control & tuning (`DOCKYARP_*`)

| Label | Purpose |
|-------|---------|
| `NETWORK_ACCESS=internal` | Restrict the route to internal client ranges (403 otherwise). |
| `DOCKYARP_CLIENT_CERT` | Client-certificate requirement (mutual TLS). |
| `DOCKYARP_AUTH_USER` / `_PASSWORD` / `_REALM` | Route Basic Auth credentials. |
| `DOCKYARP_LB` | Load-balancing policy across replicas. |
| `DOCKYARP_PRIORITY` | Route priority (higher wins). |
| `DOCKYARP_PROXY_TIMEOUT` | Per-route upstream timeout. |
| `DOCKYARP_MAX_BODY_SIZE` | Per-route maximum request body size. |

## Application configuration

The proxy's own settings are bound from configuration sections:

- **`Docker`** — endpoint, `PreferredNetwork` / `ProxyNetworks`, `ContainerFilters`, `HostAddress`.
- **`Tls`** — certificate directory, ACME settings, `SslPolicy`, `MinimumTlsVersion`, `ClientCaCertificatePath`.
- **`Security`** — `TrustDefaultCert`, `EnableHttpOnMissingCert`, HSTS defaults, `InternalRanges`,
  `HtpasswdDirectory`, `ServerHeader`.
- **`Routing`** — default host and the response for unmatched requests.
- **`Proxy`** — trusting a downstream proxy's `X-Forwarded-*` headers.

> This reference grows as capabilities are documented in depth. See the
> [parity matrix](https://github.com/gcelet/DockYARP/blob/main/openspec/backlog/parity.md) for the full,
> current feature set.
