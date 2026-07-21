# Label reference (DockYarp.Docker)

DockYarp configures itself from Docker container labels. The `VIRTUAL_*` and `LETSENCRYPT_*` labels
are **nginx-proxy compatible**; DockYarp-specific labels use the `DOCKYARP_` prefix.

## Supported labels

| Label | Required | Description | Example |
|---|---|---|---|
| `VIRTUAL_HOST` | **Yes** | Host the container is exposed on. | `app.local` |
| `VIRTUAL_PORT` | Conditional | Target container port. Required when the container exposes more than one port; inferred when exactly one is exposed. | `8080` |
| `VIRTUAL_PATH` | No | Path prefix the route matches (empty = all paths). | `/api` |
| `LETSENCRYPT_HOST` | No | Host a certificate should be obtained for (enables TLS metadata). | `app.local` |
| `LETSENCRYPT_EMAIL` | No | Contact email used when requesting the certificate. | `admin@example.com` |
| `DOCKYARP_LB` | No | Load-balancing policy: `round-robin` (default) or `least-requests`. | `least-requests` |

## Behavior

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
