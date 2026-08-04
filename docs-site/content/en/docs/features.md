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
- **Filtering**: `Docker:ContainerFilters` scopes discovery with Docker-native filters (for example, only
  containers labelled `dockyarp.enable=true`).

## Routing & load balancing

- **Matching**: requests route by `Host` (and an optional path prefix). An exact host beats a wildcard
  (`*.example.com`); `DOCKYARP_PRIORITY` orders same-host routes (higher wins).
- **Multiport**: `VIRTUAL_HOST_MULTIPORTS` maps host → path → port on a single container.
- **Path rewrite**: `VIRTUAL_DEST=/` strips the `VIRTUAL_PATH` prefix before forwarding.
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
  `SSL_POLICY` overrides the global posture.
- **Mutual TLS**: configure a client CA (`Tls:ClientCaCertificatePath`) and require client certificates per
  route with `DOCKYARP_CLIENT_CERT`.
- **Enforcement**: `HTTPS_METHOD` controls HTTP↔HTTPS behavior per host (see [Configuration](../configuration/)).

## Observability

- **Metrics**: `GET /metrics` exposes Prometheus metrics (active route/cluster counts, and more) for scraping.
- **Access log**: one structured entry per handled request. The field catalog is `Method`, `Scheme`, `Host`,
  `Path`, `Query`, `Protocol`, `RemoteIp`, `UserAgent`, `Referer`, `StatusCode`, `ElapsedMs`. Configure with
  `AccessLog:Enabled`, `AccessLog:ExcludedPathPrefixes` (default `/metrics`, `/api`), and `AccessLog:Fields`
  (an ordered selection — the structured analog of nginx `LOG_FORMAT`). Text or JSON, per the logging provider.

## Admin API

Read-only JSON endpoints for operators, protected by an API key. Set `AdminApi:ApiKey` and send it in the
`X-Api-Key` header; without a valid key the endpoints return **401** (an empty key closes the admin API).

| Endpoint | Returns |
|----------|---------|
| `GET /api/routes` | the active routing configuration (no secrets — Basic Auth is flagged, never shown) |
| `GET /api/clusters` | the active clusters and their endpoints |
| `GET /api/certs` | stored certificates (host + expiry, no private keys) |
| `GET /api/health` | overall status from real signals (discovery connectivity, certificate count, route/cluster counts) |
| `GET /api/resolve?host=<h>&path=<p>` | the effective configuration a request would match (route, transforms, TLS, security, cluster), or 404 |

```bash
curl -H "X-Api-Key: $KEY" "http://localhost:8080/api/resolve?host=app.local&path=/api"
```

## Static configuration

Point `StaticConfig:Path` at a JSON file describing `Routes`/`Clusters` to configure DockYARP without Docker
labels. It merges with discovery (static entries win) and is the sole source when `Docker:Enabled=false`.

## Custom error pages

Set `ErrorPages:Directory` to a folder of `{statusCode}.html` files (for example `404.html`, `502.html`);
DockYARP overlays them onto its own generated error responses.

## Graceful shutdown

On stop, DockYARP drains in-flight requests and stops its background workers within
`Host:ShutdownTimeoutSeconds` (default 30).
