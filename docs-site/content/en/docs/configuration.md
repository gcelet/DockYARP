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
| `SSL_POLICY` | Per-host TLS preset: `Mozilla-Modern`/`Mozilla-Intermediate`/`Mozilla-Old`, or a classic AWS ELB policy name (e.g. `ELBSecurityPolicy-TLS13-1-2-2021-06`). | global | `Mozilla-Modern` |
| `HTTPS_METHOD` | HTTP↔HTTPS behavior: `redirect` (default), `noredirect`, `nohttp`, `nohttps`. | `redirect` | `noredirect` |
| `HSTS` | Per-host `Strict-Transport-Security` value, or `off` to disable it. | global | `off` |
| `EXTERNAL_HTTPS_PORT` | External HTTPS port used in the HTTP→HTTPS redirect (behind a non-standard published port). | `443` | `8443` |
| `ENABLE_HTTP_ON_MISSING_CERT` | Per-host override: serve HTTP (no redirect) while the host has no certificate. | global (`true`) | `false` |
| `TRUST_DEFAULT_CERT` | Per-host override: may the host fall back to the default certificate (else an HTTPS request → 500). | global (`true`) | `false` |
| `DOCKYARP_HTTP2` | Per-host toggle offering HTTP/2 to clients via ALPN (`true`/`false`); `false` narrows the host to HTTP/1.1. Only narrows — enabling has no effect unless HTTP/2 is on globally. | global protocols | `false` |

### Access control, headers & tuning

| Key | Purpose | Example |
|-----|---------|---------|
| `NETWORK_ACCESS` | `internal` restricts the route to internal client ranges (403 otherwise). | `internal` |
| `DOCKYARP_CLIENT_CERT` | Client-certificate requirement (mutual TLS): `required`, `optional`, `none`/`off`. | `required` |
| `DOCKYARP_AUTH_USER` / `_PASSWORD` / `_REALM` | Route Basic Auth credentials (with an optional realm). | `admin` / `s3cret` |
| `DOCKYARP_LB` | Load-balancing policy: `round-robin` (default), `least-requests`, `power-of-two-choices`, `random`, `first-alphabetical`. | `least-requests` |
| `DOCKYARP_AFFINITY` | Session affinity ("sticky sessions"): `ip-hash`/`true` (client-IP hash, first 3 IPv4 octets — matches nginx-proxy's own `ip_hash`, needs no Data Protection), `cookie` or `custom-header` (YARP's encrypted policies — a DockYarp value-add beyond nginx-proxy, since open-source nginx has no cookie-based sticky-session mechanism; both **require** `DataProtection:CertificatePath` and are otherwise served with no affinity, logged as an error). Unset/`false` disables it (default). | `ip-hash` |
| `DOCKYARP_PRIORITY` | Route priority; higher wins when several routes match (default `0`). | `10` |
| `DOCKYARP_PROXY_TIMEOUT` | Per-route upstream timeout in seconds. | `30` |
| `DOCKYARP_MAX_BODY_SIZE` | Per-route maximum request body size in bytes. | `1048576` |
| `DOCKYARP_MAX_CONNECTIONS` | Max concurrent connections to the cluster's backend (YARP `MaxConnectionsPerServer`); unset uses YARP's default pooling. | `64` |
| `SERVER_TOKENS` | `off` suppresses the `Server` response header for the host (overrides the global value). | `off` |

### nginx-proxy namespaced label aliases

For drop-in nginx-proxy compatibility, DockYARP also accepts these namespaced **labels** as aliases (the
DockYARP-native key wins when both are set):

| nginx-proxy label | DockYARP key |
|-------------------|--------------|
| `com.github.nginx-proxy.nginx-proxy.loadbalance` | `DOCKYARP_LB` (`least_conn`→least-requests, `random`, `round_robin`); `ip_hash`/`hash $x` → `DOCKYARP_AFFINITY=ip-hash` instead (session affinity, not a load-balancing policy) |
| `com.github.nginx-proxy.nginx-proxy.ssl_verify_client` | `DOCKYARP_CLIENT_CERT` (`on`→required, `optional`→optional) |
| `com.github.nginx-proxy.nginx-proxy.trust-default-cert` | `TRUST_DEFAULT_CERT` |
| `com.github.nginx-proxy.nginx-proxy.http2.enable` | `DOCKYARP_HTTP2` |

## Application configuration

These are the proxy's own settings, bound from configuration sections. Any key can be set in `appsettings.json`
or as a **double-underscore environment variable** on the proxy container (for example
`Tls__AcceptTermsOfService=true`, `Docker__Enabled=true`). Defaults are shown.

### `Server` — data-plane ports

ACME HTTP-01 needs port 80 reachable from the certificate authority, and clients need port 443 reachable —
regardless of deployment topology. With a host port-remap (Docker's default bridge networking + `ports:`), the
non-root defaults below are published as 80/443 by Docker itself; with no such remap (macvlan, host networking)
the container must listen on 80/443 directly — see [Examples](/docs/examples/#no-host-port-remap-macvlan-host-networking).

| Key | Default | Purpose |
|-----|---------|---------|
| `HttpPort` | `8080` | Plaintext HTTP port (ACME challenge + HTTP→HTTPS redirects). |
| `HttpsPort` | `8443` | HTTPS port (per-SNI TLS). |
| `EnableProxyProtocol` | `false` | Expect a PROXY protocol header (v1/v2) on edge connections and recover the real client IP (behind an L4 balancer: NLB/HAProxy). A malformed header aborts the connection. |

### `Docker` — discovery

| Key | Default | Purpose |
|-----|---------|---------|
| `Enabled` | `false` | Turn on Docker discovery (when off, only the static configuration is applied). |
| `DockerEndpoint` | platform default | Docker API URI (`unix:///var/run/docker.sock`, `npipe://./pipe/docker_engine`, or `tcp://…`). |
| `PreferredNetwork` | — | Network whose container IP is preferred for the backend address. |
| `PreferIpv6` | `false` | Forward to a backend's IPv6 address when it has one (nginx-proxy `PREFER_IPV6_NETWORK`). A single family is chosen per network, falling back to the other when the preferred one is absent. |
| `ProxyNetworks` | auto-detected | Networks the proxy is attached to (restricts address selection to a reachable one; a backend on no shared network is skipped). When unset, DockYarp detects its own attached networks by inspecting its own container (via `HOSTNAME`). |
| `HostAddress` | — | Address used to reach host-network backends (e.g. `host.docker.internal`). |
| `ContainerFilters` | — | Docker-native filters scoping discovery, e.g. `Docker:ContainerFilters:label:0 = dockyarp.enable=true`. |
| `InitialReconnectDelay` / `MaxReconnectDelay` | `00:00:01` / `00:00:30` | Event-stream reconnect backoff. |
| `ReconcileDebounceMin` / `ReconcileDebounceMax` | `00:00:00.250` / `00:00:02` | Coalesce a burst of Docker events into one reconcile: quiet window after the last event (extended per event), capped from the burst's first event. `ReconcileDebounceMin = 0` reconciles per event. Startup/reconnect passes are immediate. |
| `CertPath` | — | Directory with `ca.pem`/`cert.pem`/`key.pem` for a remote `tcp://` daemon over TLS (the Docker `DOCKER_CERT_PATH` convention); DockYarp presents the client certificate. A socket endpoint is unaffected. |
| `TlsVerify` | `false` | Verify the daemon certificate against `CertPath`'s `ca.pem` (custom root trust). Ignored without `CertPath`. |

### `Tls` — certificates & ACME

| Key | Default | Purpose |
|-----|---------|---------|
| `CertificateDirectory` | `certs` | Directory for the certificate store and Data Protection keys. |
| `AcmeDirectoryUri` | Let's Encrypt **staging** | ACME directory endpoint (set the production URL to issue trusted certs). |
| `AcceptTermsOfService` | `false` | Must be `true` for ACME issuance. |
| `ContactEmail` | — | Default ACME contact when a host declares no `LETSENCRYPT_EMAIL`. |
| `RenewBeforeExpiry` | `30.00:00:00` | Renew a certificate this long before it expires. |
| `CheckInterval` | `12:00:00` | Provisioning / renewal check interval. |
| `Http01ChallengeEnabled` | `true` | Serve the ACME HTTP-01 challenge path. Challenges are answered by token regardless of host (a not-yet-routed host is served). `false` returns 404 on the challenge path. |
| `MinimumTlsVersion` | `Tls12` | Global TLS floor (a per-host `SSL_POLICY` overrides it). |
| `SslPolicy` | — | Global preset: `Mozilla-Modern`/`Mozilla-Intermediate`/`Mozilla-Old`, or a classic AWS ELB policy name. ELB names are clamped to the TLS 1.2 floor (TLS 1.3 for the 1.3-only policy) with best-effort ciphers; FIPS/PQ/RFC 9151 variants are not recognized. |
| `CipherSuites` | — | Explicit cipher allow-list (applied on Linux/macOS only). |
| `HttpProtocols` | `Http1AndHttp2` | Enabled HTTP protocols on the HTTPS endpoint. |
| `ClientCaCertificatePath` | — | Client CA (PEM) enabling mutual TLS. |
| `PrivateKeyEncryptionPassphrase` | — (plain) | Opt in to encrypting every stored certificate's private key at rest (`ENCRYPTED PRIVATE KEY` PEM). Plain-vs-encrypted is always decided from the key file's own PEM label, never from this setting, so an operator-provided plain key keeps loading even once set. **Security note:** this protects against someone with filesystem/volume/backup access only — it does **not** defend against `AdminApi:AllowCertificateDownload`, since DockYarp must decrypt the key itself, automatically, at startup, to serve TLS; whoever can reach the dashboard is, in practice, in the same trust domain as whatever holds this passphrase. |
| `PreviousPrivateKeyEncryptionPassphrase` | — | Fallback passphrase tried when a stored key doesn't decrypt with `PrivateKeyEncryptionPassphrase`. Set this to the outgoing value while rotating `PrivateKeyEncryptionPassphrase` to a new one, so already-encrypted keys keep loading until they are next rewritten (a renewal, or the dashboard's "Re-encrypt key" action — see `AdminApi:AllowCertificateConversion`). |

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
| `Surface` | `Disabled` | What the admin surface exposes: `Disabled` (nothing mapped, the default — a backend's own `/api/*` or `/metrics` is never shadowed), `Api` (JSON admin API + `/metrics`, no dashboard), or `ApiAndDashboard` (both). |
| `ApiKey` | — (closed) | Key required in the `X-Api-Key` header for the JSON API; empty means every request is rejected with 401. |
| `Host` | — | Dedicated host to scope the admin API (`/api/*`), `/metrics`, and the dashboard to. **Required whenever `Surface` is not `Disabled`** — the app fails to start otherwise. When set, those paths answer only on this host; on any other host they fall through to proxying (so a backend's `/api/*` is not shadowed). |
| `LetsEncrypt` | `false` | Opt in to ACME-provision a certificate for `Host` (needs `Host` set). When enabled, the admin host is provisioned and renewed like any vhost; otherwise it keeps the default/operator certificate. |
| `ContactEmail` | — | ACME contact email for the admin host; falls back to `Tls:ContactEmail` when unset. |
| `AllowCertificateDownload` | `false` | Opt in to certificate/private-key download links on `/dashboard` (needs `Surface: ApiAndDashboard`). **Security note:** once enabled, a stored certificate's private key is downloadable over HTTP, protected only by `Host`'s network isolation — no application-level authentication. Only enable this on an admin host that is genuinely not reachable from an untrusted network. |
| `AllowCertificateConversion` | `false` | Opt in to a "Convert to PEM" action on `/dashboard` for any certificate still backed by a legacy `.pfx` file (needs `Surface: ApiAndDashboard`). This also gates the "Re-encrypt key" action (see `Tls:PrivateKeyEncryptionPassphrase` below), which additionally requires that passphrase to be configured. These are the **only mutating actions** the admin surface exposes — each only rewrites the on-disk format of an already-served certificate (no re-provisioning, no change to what's served), protected by the same anti-forgery mechanism as any other Razor Pages form submission. Gated independently of `AllowCertificateDownload`. |

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
> [parity matrix]({{< repo-file "openspec/backlog/parity.md" >}}) for the full feature set.
