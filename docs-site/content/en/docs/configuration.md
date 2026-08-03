---
title: Configuration
weight: 2
description: Container labels and application configuration.
---

DockYARP is configured two ways: **container configuration** (per backend, nginx-proxy compatible) and
**application configuration** (the proxy's own settings).

## Container configuration (labels or environment variables)

Every key below can be set as a **container label** *or* as an **environment variable** on the same container.
When a key is set both ways, the **environment variable wins** — environment variables are nginx-proxy's
canonical channel and the label is the fallback. The `VIRTUAL_*`, `LETSENCRYPT_*`, `CERT_NAME`, `SSL_POLICY`,
`HTTPS_METHOD`, `HSTS`, `NETWORK_ACCESS`, `SERVER_TOKENS`, and `EXTERNAL_HTTPS_PORT` keys are nginx-proxy
compatible; DockYARP's own keys use the `DOCKYARP_` prefix.

### Routing

| Key | Purpose | Example |
|-----|---------|---------|
| `VIRTUAL_HOST` | Host(s) to route to the container (comma-separated for several). | `app.local,www.app.local` |
| `VIRTUAL_PORT` | Target container port (required when the container exposes several ports; inferred for one). | `8080` |
| `VIRTUAL_PATH` | Optional path prefix the route matches (empty = all paths). | `/api` |
| `VIRTUAL_PROTO` | Backend protocol: `http` (default), `https`, `grpc`, `grpcs`. | `https` |
| `VIRTUAL_DEST` | Rewrite the matched path before forwarding; `/` strips the `VIRTUAL_PATH` prefix. | `/` |
| `VIRTUAL_HOST_MULTIPORTS` | YAML `host: { path: { port, proto, dest } }`; replaces `VIRTUAL_HOST`/`VIRTUAL_PORT` for multi-port containers. | _(see below)_ |

### TLS

| Key | Purpose | Default | Example |
|-----|---------|---------|---------|
| `LETSENCRYPT_HOST` | Host to provision an ACME certificate for (enables TLS metadata). | — | `app.local` |
| `LETSENCRYPT_EMAIL` | Contact email for the ACME account. | global | `admin@example.com` |
| `CERT_NAME` | Pin the host to a named shared (SAN/wildcard) certificate; the host is not ACME-provisioned. | — | `wildcard` |
| `SSL_POLICY` | Per-host TLS preset: `Mozilla-Modern`, `Mozilla-Intermediate`, `Mozilla-Old`. | global | `Mozilla-Modern` |
| `HTTPS_METHOD` | HTTP↔HTTPS behavior: `redirect` (default), `noredirect`, `nohttp`, `nohttps`. | `redirect` | `noredirect` |
| `HSTS` | Per-host `Strict-Transport-Security` value, or `off` to disable it. | global | `off` |
| `EXTERNAL_HTTPS_PORT` | External HTTPS port used in the HTTP→HTTPS redirect (behind a non-standard published port). | `443` | `8443` |
| `ENABLE_HTTP_ON_MISSING_CERT` | Per-host override: serve HTTP (no redirect) while the host has no certificate. | global (`true`) | `false` |
| `TRUST_DEFAULT_CERT` | Per-host override: may the host fall back to the default certificate (else an HTTPS request → 500). | global (`true`) | `false` |

### Access control, headers & tuning

| Key | Purpose | Example |
|-----|---------|---------|
| `NETWORK_ACCESS` | `internal` restricts the route to internal client ranges (403 otherwise). | `internal` |
| `DOCKYARP_CLIENT_CERT` | Client-certificate requirement (mutual TLS): `required`, `optional`, `none`/`off`. | `required` |
| `DOCKYARP_AUTH_USER` / `_PASSWORD` / `_REALM` | Route Basic Auth credentials (with an optional realm). | `admin` / `s3cret` |
| `DOCKYARP_LB` | Load-balancing policy: `round-robin` (default), `least-requests`, `power-of-two-choices`, `random`, `first-alphabetical`. | `least-requests` |
| `DOCKYARP_PRIORITY` | Route priority; higher wins when several routes match (default `0`). | `10` |
| `DOCKYARP_PROXY_TIMEOUT` | Per-route upstream timeout in seconds. | `30` |
| `DOCKYARP_MAX_BODY_SIZE` | Per-route maximum request body size in bytes. | `1048576` |
| `SERVER_TOKENS` | `off` suppresses the `Server` response header for the host (overrides the global value). | `off` |

### nginx-proxy namespaced label aliases

For drop-in nginx-proxy compatibility, DockYARP also accepts these namespaced **labels** as aliases (the
DockYARP-native key wins when both are set):

| nginx-proxy label | DockYARP key |
|-------------------|--------------|
| `com.github.nginx-proxy.nginx-proxy.loadbalance` | `DOCKYARP_LB` (`least_conn`→least-requests, `random`, `round_robin`) |
| `com.github.nginx-proxy.nginx-proxy.ssl_verify_client` | `DOCKYARP_CLIENT_CERT` (`on`→required, `optional`→optional) |
| `com.github.nginx-proxy.nginx-proxy.trust-default-cert` | `TRUST_DEFAULT_CERT` |

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
