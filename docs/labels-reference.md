# Label reference (DockYarp.Docker)

DockYarp configures itself from a Docker container's **labels or environment variables** — every key below can
be set either way, and when a key is set both ways the **environment variable wins** (nginx-proxy's canonical
channel; the label is the fallback). The `VIRTUAL_*`, `LETSENCRYPT_*`, `CERT_NAME`, `SSL_POLICY`, `HTTPS_METHOD`,
`HSTS`, `NETWORK_ACCESS`, `SERVER_TOKENS`, and `EXTERNAL_HTTPS_PORT` keys are **nginx-proxy compatible**;
DockYarp-specific keys use the `DOCKYARP_` prefix.

## Supported keys (label or environment variable)

| Key | Required | Description | Example |
|---|---|---|---|
| `VIRTUAL_HOST` | **Yes**\* | Host(s) the container is exposed on; comma-separated for several. | `app.local,www.app.local` |
| `VIRTUAL_HOST_MULTIPORTS` | No | YAML `host: { path: { port, proto, dest } }`; replaces `VIRTUAL_HOST`/`VIRTUAL_PORT` for multi-port containers. | _(see below)_ |
| `VIRTUAL_PORT` | Conditional | Target container port. Required when the container exposes more than one port; inferred when exactly one is exposed. | `8080` |
| `VIRTUAL_PATH` | No | Path prefix the route matches (empty = all paths). | `/api` |
| `VIRTUAL_PROTO` | No | Backend transport scheme: `http` (default), `https`, `grpc`, `grpcs`. | `https` |
| `VIRTUAL_DEST` | No | Rewrites the matched path before forwarding; `/` strips the `VIRTUAL_PATH` prefix. | `/` |
| `LETSENCRYPT_HOST` | No | Host a certificate should be obtained for (enables TLS metadata). | `app.local` |
| `LETSENCRYPT_EMAIL` | No | Contact email used when requesting the certificate. | `admin@example.com` |
| `DOCKYARP_ACME_CHALLENGE` | No | ACME challenge type: `http-01` (default) or `dns-01` (required for a wildcard `LETSENCRYPT_HOST`; needs `Tls:DnsUpdate*` configured — see `docs/tls-acme.md`). | `dns-01` |
| `CERT_NAME` | No | Pin the host to a named shared (SAN/wildcard) certificate; the host is not ACME-provisioned. | `wildcard` |
| `SSL_POLICY` | No | Per-host TLS preset overriding the global posture: `Mozilla-Modern`/`Mozilla-Intermediate`/`Mozilla-Old`, or a classic AWS ELB policy name (e.g. `ELBSecurityPolicy-TLS13-1-2-2021-06`; clamped to the TLS 1.2 floor, best-effort ciphers, FIPS/PQ/RFC 9151 variants not recognized). | `Mozilla-Modern` |
| `HTTPS_METHOD` | No | HTTP↔HTTPS behavior: `redirect` (default), `noredirect`, `nohttp`, `nohttps`. | `noredirect` |
| `HSTS` | No | Per-host `Strict-Transport-Security` value, or `off` to disable HSTS for the host. | `off` |
| `EXTERNAL_HTTPS_PORT` | No | External HTTPS port used in the HTTP→HTTPS redirect target (default 443). | `8443` |
| `ENABLE_HTTP_ON_MISSING_CERT` | No | Per-host override: serve HTTP (no redirect) while the host has no certificate (default from global). | `false` |
| `TRUST_DEFAULT_CERT` | No | Per-host override: may the host fall back to the default certificate; else an HTTPS request is refused with 500 (default from global). | `false` |
| `DOCKYARP_HTTP2` | No | Per-host toggle offering HTTP/2 to clients via ALPN; `false` narrows the host to HTTP/1.1 (only narrows — default from the global protocols). | `false` |
| `NETWORK_ACCESS` | No | `internal` restricts the route to internal client ranges (403 otherwise). | `internal` |
| `SERVER_TOKENS` | No | `off` suppresses the `Server` response header for the host (overrides the global value). | `off` |
| `DOCKYARP_LB` | No | Load-balancing policy: `round-robin` (default), `least-requests`, `power-of-two-choices`, `random`, `first-alphabetical`. | `least-requests` |
| `DOCKYARP_PRIORITY` | No | Route priority (DockYarp extension); orders same-host routes, higher wins (default `0`). | `10` |
| `DOCKYARP_CLIENT_CERT` | No | Client-certificate (mTLS) requirement: `required` (403/handshake failure on a missing/untrusted/revoked cert), `optional` (never rejects on the cert's trust outcome — `X-SSL-Client-Verify: SUCCESS`/`FAILED`/`NONE` lets the backend decide), or `none`/`off` (default). | `required` |
| `DOCKYARP_PROXY_TIMEOUT` | No | Proxy request timeout in seconds (cluster activity timeout). | `30` |
| `DOCKYARP_MAX_BODY_SIZE` | No | Maximum request body size in bytes for the route. | `1048576` |
| `DOCKYARP_MAX_CONNECTIONS` | No | Max concurrent connections to the cluster's backend (YARP `MaxConnectionsPerServer`); unset uses YARP's default pooling. | `64` |
| `DOCKYARP_AUTH_USER` | No | Basic Auth username protecting the route (with `DOCKYARP_AUTH_PASSWORD`). | `admin` |
| `DOCKYARP_AUTH_PASSWORD` | No | Basic Auth password. | `s3cret` |
| `DOCKYARP_AUTH_REALM` | No | Optional Basic Auth realm shown in the challenge. | `Admin area` |

### nginx-proxy namespaced label aliases

DockYarp also accepts these nginx-proxy namespaced **labels** as aliases (the DockYarp-native key wins when both
are set): `com.github.nginx-proxy.nginx-proxy.loadbalance` → `DOCKYARP_LB` (nginx directives `least_conn`/
`random`/`round_robin` translate), `com.github.nginx-proxy.nginx-proxy.ssl_verify_client` → `DOCKYARP_CLIENT_CERT`
(`on`→required, `optional`→optional), `com.github.nginx-proxy.nginx-proxy.trust-default-cert` → `TRUST_DEFAULT_CERT`,
`com.github.nginx-proxy.nginx-proxy.http2.enable` → `DOCKYARP_HTTP2`.

## Behavior

- **Basic Auth**: `DOCKYARP_AUTH_USER` and `DOCKYARP_AUTH_PASSWORD` protect the route (visible via
  `docker inspect`). Both are required together; if only one is present the route is left unprotected and a
  warning is logged.

- **Multiple hosts**: a comma-separated `VIRTUAL_HOST` (whitespace trimmed, empty entries ignored) maps the
  container to one route per host, each sharing its port, path, TLS, and auth settings.
- **Raw IP host**: `VIRTUAL_HOST` may be a bare IPv4 address (e.g. `192.0.2.10`); it is matched exactly against
  the request `Host`. IPv6 literals are not yet supported (the bracketed `Host` form needs normalization).
- **Multi-port** (\*): `VIRTUAL_HOST_MULTIPORTS` supersedes `VIRTUAL_HOST`/`VIRTUAL_PORT` — a YAML mapping of
  `host → path → { port, proto, dest }` maps each entry to its own route/cluster (on the entry's port and
  scheme). Container-level attributes (auth, LB, priority, timeout, body size, client cert, and TLS when the
  host is a `LETSENCRYPT_HOST`) apply to the generated routes; a non-empty `dest` strips the path prefix.

  ```yaml
  # VIRTUAL_HOST_MULTIPORTS
  app.local:
    "/":
      port: 8080
    "/api":
      port: 9000
      proto: https
      dest: "/"
  ```
- **Backend scheme**: `VIRTUAL_PROTO` selects how the proxy reaches the container — `http` (default) or
  `https`. Unsupported values (e.g. `fastcgi`) fall back to `http` and are logged.
- **Priority** *(DockYarp extension — no nginx-proxy equivalent)*: `DOCKYARP_PRIORITY` orders routes
  **for the same host** when several could match — a higher value wins (it maps to a lower YARP route order).
  It does **not** override host specificity: an exact host still beats a higher-priority wildcard (matching
  nginx `server_name`). A non-numeric value falls back to `0` and is logged.
- **Proxy tuning**: `DOCKYARP_PROXY_TIMEOUT` (seconds) sets the cluster's YARP activity timeout;
  `DOCKYARP_MAX_BODY_SIZE` (bytes) caps the request body for the route; `DOCKYARP_MAX_CONNECTIONS` caps
  concurrent connections to the backend (YARP `MaxConnectionsPerServer`, else default pooling). Non-numeric or
  non-positive values are ignored and logged.
- **Path rewrite**: `VIRTUAL_DEST` rewrites the matched path before forwarding. Currently the supported
  form is `VIRTUAL_DEST=/`, which strips the `VIRTUAL_PATH` prefix (so `/api/x` reaches the backend as
  `/x`); richer rewrites are not yet supported.
- **Port inference**: with no `VIRTUAL_PORT`, if the container exposes exactly one port it is used;
  otherwise the container is skipped with a warning (ambiguous).
- **Replicas**: containers sharing the same `VIRTUAL_HOST` are aggregated into a single cluster, one
  endpoint per container (keyed by container id).
- **TLS**: `LETSENCRYPT_HOST` populates per-host TLS metadata (with `LETSENCRYPT_EMAIL`) consumed later by
  the TLS capability; it does not itself obtain a certificate.
- **Validation**: invalid or incomplete labels cause that container to be skipped and logged; other
  containers are unaffected.

## Example (docker-compose)

```yaml
services:
  web:
    image: my/web
    labels:
      VIRTUAL_HOST: app.local
      VIRTUAL_PORT: "8080"
      VIRTUAL_PATH: /
      LETSENCRYPT_HOST: app.local
      LETSENCRYPT_EMAIL: admin@example.com
      DOCKYARP_LB: round-robin
```

Scaling `web` to several replicas adds each replica as an endpoint of the same `app.local` cluster.
