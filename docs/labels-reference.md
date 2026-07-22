# Label reference (DockYarp.Docker)

DockYarp configures itself from Docker container labels. The `VIRTUAL_*` and `LETSENCRYPT_*` labels
are **nginx-proxy compatible**; DockYarp-specific labels use the `DOCKYARP_` prefix.

## Supported labels

| Label | Required | Description | Example |
|---|---|---|---|
| `VIRTUAL_HOST` | **Yes** | Host(s) the container is exposed on; comma-separated for several. | `app.local,www.app.local` |
| `VIRTUAL_PORT` | Conditional | Target container port. Required when the container exposes more than one port; inferred when exactly one is exposed. | `8080` |
| `VIRTUAL_PATH` | No | Path prefix the route matches (empty = all paths). | `/api` |
| `VIRTUAL_PROTO` | No | Backend transport scheme: `http` (default) or `https`. | `https` |
| `VIRTUAL_DEST` | No | Rewrites the matched path before forwarding; `/` strips the `VIRTUAL_PATH` prefix. | `/` |
| `LETSENCRYPT_HOST` | No | Host a certificate should be obtained for (enables TLS metadata). | `app.local` |
| `LETSENCRYPT_EMAIL` | No | Contact email used when requesting the certificate. | `admin@example.com` |
| `HTTPS_METHOD` | No | HTTP↔HTTPS behavior: `redirect` (default), `noredirect`, `nohttp`, `nohttps`. | `noredirect` |
| `HSTS` | No | Per-host `Strict-Transport-Security` value, or `off` to disable HSTS for the host. | `off` |
| `DOCKYARP_LB` | No | Load-balancing policy: `round-robin` (default) or `least-requests`. | `least-requests` |
| `DOCKYARP_PRIORITY` | No | Route priority; higher wins when several routes could match (default `0`). | `10` |
| `DOCKYARP_AUTH_USER` | No | Basic Auth username protecting the route (with `DOCKYARP_AUTH_PASSWORD`). | `admin` |
| `DOCKYARP_AUTH_PASSWORD` | No | Basic Auth password. | `s3cret` |
| `DOCKYARP_AUTH_REALM` | No | Optional Basic Auth realm shown in the challenge. | `Admin area` |

## Behavior

- **Basic Auth**: `DOCKYARP_AUTH_USER` and `DOCKYARP_AUTH_PASSWORD` protect the route (visible via
  `docker inspect`). Both are required together; if only one is present the route is left unprotected and a
  warning is logged.

- **Multiple hosts**: a comma-separated `VIRTUAL_HOST` (whitespace trimmed, empty entries ignored) maps the
  container to one route per host, each sharing its port, path, TLS, and auth settings.
- **Backend scheme**: `VIRTUAL_PROTO` selects how the proxy reaches the container — `http` (default) or
  `https`. Unsupported values (e.g. `fastcgi`) fall back to `http` and are logged.
- **Priority**: `DOCKYARP_PRIORITY` orders routes when several could match — a higher value wins (it
  maps to a lower YARP route order). A non-numeric value falls back to `0` and is logged.
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
