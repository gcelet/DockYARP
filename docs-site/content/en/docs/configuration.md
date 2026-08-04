---
title: Configuration
weight: 2
description: Container labels and application configuration.
---

DockYARP is configured two ways: **container configuration** (per backend, nginx-proxy compatible) and
**application configuration** (the proxy's own settings). For what these settings *do* at runtime, see
[Features](../features/).

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
| `VIRTUAL_PATH` | Path the route matches: a prefix, or a `~`-prefixed regular expression (`~^/(a|b)/`). Empty = all paths. | `/api` |
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

These are the proxy's own settings, bound from configuration sections. Any key can be set in `appsettings.json`
or as a **double-underscore environment variable** on the proxy container (for example
`Tls__AcceptTermsOfService=true`, `Docker__Enabled=true`). Defaults are shown.

### `Server` — data-plane ports

| Key | Default | Purpose |
|-----|---------|---------|
| `HttpPort` | `8080` | Plaintext HTTP port (ACME challenge + HTTP→HTTPS redirects). |
| `HttpsPort` | `8443` | HTTPS port (per-SNI TLS). |

### `Docker` — discovery

| Key | Default | Purpose |
|-----|---------|---------|
| `Enabled` | `false` | Turn on Docker discovery (when off, only the static configuration is applied). |
| `DockerEndpoint` | platform default | Docker API URI (`unix:///var/run/docker.sock`, `npipe://./pipe/docker_engine`, or `tcp://…`). |
| `PreferredNetwork` | — | Network whose container IP is preferred for the backend address. |
| `ProxyNetworks` | — | Networks the proxy is attached to (restricts address selection to a reachable one). |
| `HostAddress` | — | Address used to reach host-network backends (e.g. `host.docker.internal`). |
| `ContainerFilters` | — | Docker-native filters scoping discovery, e.g. `Docker:ContainerFilters:label:0 = dockyarp.enable=true`. |
| `InitialReconnectDelay` / `MaxReconnectDelay` | `00:00:01` / `00:00:30` | Event-stream reconnect backoff. |

### `Tls` — certificates & ACME

| Key | Default | Purpose |
|-----|---------|---------|
| `CertificateDirectory` | `certs` | Directory for the certificate store and Data Protection keys. |
| `AcmeDirectoryUri` | Let's Encrypt **staging** | ACME directory endpoint (set the production URL to issue trusted certs). |
| `AcceptTermsOfService` | `false` | Must be `true` for ACME issuance. |
| `ContactEmail` | — | Default ACME contact when a host declares no `LETSENCRYPT_EMAIL`. |
| `RenewBeforeExpiry` | `30.00:00:00` | Renew a certificate this long before it expires. |
| `CheckInterval` | `12:00:00` | Provisioning / renewal check interval. |
| `MinimumTlsVersion` | `Tls12` | Global TLS floor (a per-host `SSL_POLICY` overrides it). |
| `SslPolicy` | — | Global preset: `Mozilla-Modern` / `Mozilla-Intermediate` / `Mozilla-Old`. |
| `CipherSuites` | — | Explicit cipher allow-list (applied on Linux/macOS only). |
| `HttpProtocols` | `Http1AndHttp2` | Enabled HTTP protocols on the HTTPS endpoint. |
| `ClientCaCertificatePath` | — | Client CA (PEM) enabling mutual TLS. |

### `Security`

| Key | Default | Purpose |
|-----|---------|---------|
| `EnableHsts` | `true` | Emit HSTS on HTTPS responses. |
| `HstsMaxAge` | `365.00:00:00` | HSTS `max-age`. |
| `HstsIncludeSubDomains` / `HstsPreload` | `false` / `false` | HSTS directives. |
| `TrustDefaultCert` | `true` | May a host with no real cert fall back to the default one (else an HTTPS request → 500). Per-host override: `TRUST_DEFAULT_CERT`. |
| `EnableHttpOnMissingCert` | `true` | Serve HTTP (no redirect) while a host has no certificate. Per-host override: `ENABLE_HTTP_ON_MISSING_CERT`. |
| `FrameOptions` | `DENY` | `X-Frame-Options` value. |
| `ReferrerPolicy` | `no-referrer` | `Referrer-Policy` value. |
| `ServerHeader` | — (suppressed) | Custom `Server` header value (a per-host `SERVER_TOKENS=off` opts out). |
| `InternalRanges` | private ranges + `::1` | CIDRs treated as internal for `NETWORK_ACCESS=internal`. |
| `HtpasswdDirectory` | — | Directory of Apache htpasswd files enabling file-based Basic Auth. |
| `HtpasswdReloadInterval` | `00:00:30` | How often the htpasswd directory is reloaded. |

### `Routing`

| Key | Default | Purpose |
|-----|---------|---------|
| `DefaultHost` | — | Host whose route also serves requests matching no other host. |
| `DefaultResponseStatusCode` | `404` | Status returned when a request matches no route and no default host. |
| `DefaultResponseLocation` | — | Optional redirect `Location` for unmatched requests (`$scheme`/`$host`/`$request_uri`). |

### `Proxy`

| Key | Default | Purpose |
|-----|---------|---------|
| `TrustDownstreamProxy` | `true` | Append to inbound `X-Forwarded-*` headers (trusted) rather than replacing them. |

### `AccessLog`

| Key | Default | Purpose |
|-----|---------|---------|
| `Enabled` | `true` | Emit one access-log entry per request. |
| `ExcludedPathPrefixes` | `/metrics`, `/api` | Request path prefixes excluded from access logging. |
| `Fields` | default set | Ordered field selection (the structured analog of nginx `LOG_FORMAT`). |

### `AdminApi`

| Key | Default | Purpose |
|-----|---------|---------|
| `ApiKey` | — (closed) | Key required in the `X-Api-Key` header; empty closes the admin API. |

### `Compression`

| Key | Default | Purpose |
|-----|---------|---------|
| `Enabled` | `true` | gzip/brotli for compressible responses; set `false` to disable. |

### `DataProtection`

| Key | Default | Purpose |
|-----|---------|---------|
| `CertificatePath` / `CertificatePassword` | — | PFX used to encrypt the persisted key ring at rest (store it **outside** the `certs` volume). |

### `Host`

| Key | Default | Purpose |
|-----|---------|---------|
| `ShutdownTimeoutSeconds` | `30` | Graceful-shutdown drain timeout. |

> See the [container configuration](#container-configuration-labels-or-environment-variables) above for per-backend
> labels/env vars, and the
> [parity matrix](https://github.com/gcelet/DockYARP/blob/main/openspec/backlog/parity.md) for the full feature set.
