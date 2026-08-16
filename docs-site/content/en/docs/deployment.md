---
title: Deployment
weight: 7
description: Running DockYARP in production.
---

## Docker Compose

DockYARP's runtime image is non-root and cannot open `/var/run/docker.sock` directly — it reaches the Docker API
through a read-only [socket proxy](https://github.com/Tecnativa/docker-socket-proxy):

```yaml
services:
  dockerproxy:
    image: tecnativa/docker-socket-proxy
    environment:
      CONTAINERS: "1"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
    restart: unless-stopped

  dockyarp:
    image: gcelet/dockyarp   # or dockyarp:local for a local build
    ports:
      - "80:8080"
      - "443:8443"
    environment:
      Docker__Enabled: "true"
      Docker__DockerEndpoint: "tcp://dockerproxy:2375"
      AdminApi__ApiKey: "change-me"
    volumes:
      - dockyarp-certs:/certs
    depends_on: [dockerproxy]
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

- Only the socket proxy mounts `/var/run/docker.sock`, and read-only (`:ro`); DockYARP itself never touches it.
- Persist the certificate directory (here `/certs`) so ACME certificates survive restarts.
- Publish host ports **80** and **443** onto the container's **8080**/**8443** (its non-root defaults); port 80
  also serves the ACME HTTP-01 challenge and HTTP→HTTPS redirects.
- Scope discovery on busy hosts with `Docker:ContainerFilters` (e.g. only containers carrying a given label).

> Reachability, host-network backends, and multi-network setups are covered under
> [Configuration](/docs/configuration/) (`Docker:ProxyNetworks`, `Docker:HostAddress`).
