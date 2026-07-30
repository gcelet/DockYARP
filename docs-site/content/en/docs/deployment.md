---
title: Deployment
weight: 4
description: Running DockYARP in production.
---

## Docker Compose

```yaml
services:
  dockyarp:
    image: dockyarp
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
      - dockyarp-certs:/certs
    restart: unless-stopped

  web:
    image: my-app
    labels:
      - "VIRTUAL_HOST=app.example.io"
      - "VIRTUAL_PORT=8080"
      - "LETSENCRYPT_HOST=app.example.io"
      - "LETSENCRYPT_EMAIL=admin@example.io"

volumes:
  dockyarp-certs:
```

## Notes

- Mount the Docker socket read-only (`:ro`); DockYARP only needs to observe containers.
- Persist the certificate directory (here `/certs`) so ACME certificates survive restarts.
- Publish ports **80** and **443**; port 80 also serves the ACME HTTP-01 challenge and HTTP→HTTPS redirects.
- Scope discovery on busy hosts with `Docker:ContainerFilters` (e.g. only containers carrying a given label).

> Reachability, host-network backends, and multi-network setups are covered under
> [Configuration](/docs/configuration/) (`Docker:ProxyNetworks`, `Docker:HostAddress`).
