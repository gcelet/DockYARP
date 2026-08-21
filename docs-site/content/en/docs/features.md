---
title: Features
weight: 3
description: How DockYARP discovers, routes, secures, observes, and operates at runtime.
---

DockYARP's runtime behavior, feature by feature. See [Configuration](../configuration/) for the labels,
environment variables, and application settings referenced here.

## Discovery

DockYARP watches the Docker daemon and builds its routes from each container's labels/environment variables,
reconciling live as containers start and stop.

- **Health-aware**: a container failing its Docker health check is excluded, so healthy replicas keep serving.
- **Network selection**: on multi-network setups, `Docker:PreferredNetwork` picks the backend address;
  `Docker:ProxyNetworks` restricts selection to a network the proxy shares (reachable), and `Docker:HostAddress`
  reaches host-network backends (which have no container IP).
- **IPv6**: the edge listens dual-stack by default (IPv6 **and** IPv4 clients are served — no toggle needed, unlike
  nginx-proxy's opt-in `ENABLE_IPV6`). Set `Docker:PreferIpv6` to forward to a backend's IPv6 address when it has one
  (nginx-proxy `PREFER_IPV6_NETWORK`); a single family is used per backend, so there are no duplicate endpoints.
- **Filtering**: `Docker:ContainerFilters` scopes discovery with Docker-native filters (for example, only
  containers labelled `dockyarp.enable=true`).

## Routing & load balancing

- **Matching**: requests route by `Host` (and an optional path prefix). An exact host beats a wildcard
  (`*.example.com`); `DOCKYARP_PRIORITY` orders same-host routes (higher wins).
- **Multiport**: `VIRTUAL_HOST_MULTIPORTS` maps host → path → port on a single container.
- **Path rewrite**: `VIRTUAL_DEST=/` strips the `VIRTUAL_PATH` prefix before forwarding.
- **Regex paths**: a `~`-prefixed `VIRTUAL_PATH` (for example `~^/(app1|alt1)/`) matches by regular expression; a
  prefix path beats a regex path, and `VIRTUAL_DEST` does not apply to a regex path.
- **Replicas & policy**: containers sharing a `VIRTUAL_HOST` form one cluster; `DOCKYARP_LB` selects the
  load-balancing policy — `round-robin` (default), `least-requests`, `power-of-two-choices`, `random`, or
  `first-alphabetical`.
- **Default host & fallback**: `Routing:DefaultHost` catches unknown hosts; otherwise an unmatched request gets
  `Routing:DefaultResponseStatusCode` (404 by default), or an optional redirect via `Routing:DefaultResponseLocation`.

## TLS & ACME

DockYARP terminates TLS and can obtain certificates automatically.

- **Automatic certificates**: a host with `LETSENCRYPT_HOST` is provisioned over ACME (HTTP-01) and renewed
  before expiry; a host with no certificate gets a self-signed fallback.
- **Provided & shared certs**: drop PEM/PFX files in the certificate directory; `CERT_NAME` pins a host to a
  shared SAN/wildcard certificate (not ACME-provisioned).
- **Per-connection policy**: the certificate, TLS version, and ciphers are selected per SNI host — a per-host
  `SSL_POLICY` overrides the global posture. Presets are the Mozilla ones (`Modern`/`Intermediate`/`Old`) and the
  classic AWS ELB policy names (clamped to DockYARP's TLS 1.2 floor, best-effort ciphers).
- **Mutual TLS**: configure a client CA (`Tls:ClientCaCertificatePath`) and require client certificates per
  route with `DOCKYARP_CLIENT_CERT`. When a verified client certificate is presented, its identity is passed to the
  backend as `X-SSL-Client-Verify: SUCCESS`, `X-SSL-Client-S-DN` (subject), and `X-SSL-Client-I-DN` (issuer);
  client-supplied `X-SSL-Client-*` headers are always stripped (anti-spoof).
- **Enforcement**: `HTTPS_METHOD` controls HTTP↔HTTPS behavior per host (see [Configuration](../configuration/)).

## Access control

- **Basic Auth**: protect a route with `DOCKYARP_AUTH_USER`/`DOCKYARP_AUTH_PASSWORD` labels, **or** with mounted
  Apache **htpasswd files** (`Security:HtpasswdDirectory`) — a file named `<host>` protects that host and
  `<host>_<sha1(path)>` protects a specific path. bcrypt, apr1, and SHA1 (`{SHA}`) hashes are verified, and the
  files are reloaded live. A request is allowed if it matches a label credential **or** any htpasswd entry.
- **Internal-only**: `NETWORK_ACCESS=internal` restricts a route to the internal client ranges
  (`Security:InternalRanges`); other clients get `403`.
- **Mutual TLS**: require client certificates per route with `DOCKYARP_CLIENT_CERT` (see [TLS & ACME](#tls--acme)).

## Proxying

- **Forwarded headers**: proxied requests carry `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`,
  `X-Forwarded-Port`, `X-Real-IP`, `X-Forwarded-Ssl` (`on`/`off`, matching the forwarded scheme), and
  `X-Original-URI` (the original path + query), plus the original `Host`. Client-supplied `X-Forwarded-*` values
  are trusted and appended by default; set `Proxy:TrustDownstreamProxy=false` to replace them with the real
  connection values (`X-Original-URI` is always the real request URI).
- **Compression**: responses with compressible content types are gzip/brotli-compressed when the client sends
  `Accept-Encoding` (on by default; disable with `Compression:Enabled=false`). An already-encoded upstream
  response is never double-compressed.
- **httpoxy mitigation**: a client-supplied `Proxy` request header is stripped before the request is forwarded
  to the backend.

## Observability

- **Metrics**: `GET /metrics` exposes Prometheus metrics (active route/cluster counts, and more) for scraping.
- **Access log**: one structured entry per handled request. The field catalog is `Method`, `Scheme`, `Host`,
  `Path`, `Query`, `Protocol`, `RemoteIp`, `UserAgent`, `Referer`, `StatusCode`, `ElapsedMs`. Configure with
  `AccessLog:Enabled`, `AccessLog:ExcludedPathPrefixes` (default `/metrics`, `/api`), and `AccessLog:Fields`
  (an ordered selection — the structured analog of nginx `LOG_FORMAT`). Text or JSON, per the logging provider.

## Admin API

The admin surface — the JSON API below, the dashboard, and `/metrics` — is off by default
(`AdminApi:Surface: Disabled`). Set it to `Api` (JSON endpoints + `/metrics`, no dashboard) or `ApiAndDashboard`
to turn it on; either value requires `AdminApi:Host` to be set (the app fails to start otherwise), so the
surface is always scoped to a dedicated host, never shadowing a backend's own paths.

Read-only JSON endpoints for operators, protected by an API key. Set `AdminApi:ApiKey` and send it in the
`X-Api-Key` header; without a valid key the endpoints return **401**.

| Endpoint | Returns |
|----------|---------|
| `GET /api/version` | the running build's version (git-derived) |
| `GET /api/routes` | the active routing configuration (no secrets — Basic Auth is flagged, never shown) |
| `GET /api/clusters` | the active clusters and their endpoints |
| `GET /api/certs` | stored certificates (host + expiry, no private keys) |
| `GET /api/health` | overall status from real signals (discovery connectivity, certificate count, route/cluster counts) |
| `GET /api/resolve?host=<h>&path=<p>` | the effective configuration a request would match (route, transforms, TLS, security, cluster), or 404 |

```bash
curl -H "X-Api-Key: $KEY" "http://localhost:8080/api/resolve?host=app.local&path=/api"
```

### Admin dashboard

`GET /dashboard` renders a light, read-only HTML view of the same data (current routes/clusters, certificate
expiry, overall health) — an at-a-glance macro view, not a replacement for `/metrics` or the JSON API. It
reads its data server-side and never calls `/api/*` from the browser, so **it carries no application-level
authentication of its own** — unlike `/api/*`, no API key protects it. It only serves when
`AdminApi:Surface` is `ApiAndDashboard`; set it to `Api` instead to keep the JSON API without the dashboard, or
leave `Surface` at its default (`Disabled`) to serve neither.

Setting `AdminApi:AllowCertificateDownload: true` (default `false`) adds certificate and private-key download
links to the dashboard's certificate table. **This is opt-in for a reason**: once enabled, a stored certificate's
private key becomes downloadable over HTTP, protected only by the same network isolation (`AdminApi:Host`) as
the rest of the admin surface — no additional login or token is required. Only turn this on when the admin host
genuinely isn't reachable from a network you don't trust.

Setting `AdminApi:AllowCertificateConversion: true` (default `false`) adds a "Re-encrypt key" action to the
dashboard's certificate table once `Tls:PrivateKeyEncryptionPassphrase` is also set (see [Configuration →
`Tls`](configuration.md)): it rewrites a host's stored private key under the current passphrase, covering both
a first-time enable (an existing plain key) and a passphrase rotation. Hosts whose key is not yet under the
current passphrase are flagged with a "needs re-encryption" badge. Note that encrypting the key this way does
**not** defend against `AllowCertificateDownload` above — DockYarp decrypts the key itself, automatically, at
startup, to serve TLS, so it only protects against someone with filesystem/volume/backup access.

## Static configuration

Point `StaticConfig:Path` at a JSON file with `Routes`, `Clusters`, and per-host `Overrides` to configure
DockYARP without Docker labels. It merges with discovery (a static route replaces a discovered one for the same
host/path) and is the sole source when `Docker:Enabled=false`. An `Overrides` entry injects response headers for
a host — or `default` for hosts without a specific override:

```json
{
  "Clusters": [{ "Id": "app", "Addresses": ["http://app:8080"] }],
  "Routes": [{ "Host": "app.local", "Path": "/", "Cluster": "app" }],
  "Overrides": [{ "Host": "default", "ResponseHeaders": { "X-Powered-By": "DockYARP" } }]
}
```

## Custom error pages

Set `ErrorPages:Directory` to a folder of `{statusCode}.html` files (for example `404.html`, `502.html`);
DockYARP overlays them onto its own generated error responses.

## Graceful shutdown

On stop, DockYARP drains in-flight requests and stops its background workers within
`Host:ShutdownTimeoutSeconds` (default 30).
