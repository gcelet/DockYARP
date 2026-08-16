---
title: Getting started
weight: 1
description: Run DockYARP and expose your first container.
---

## Run the proxy

DockYARP watches the Docker socket and routes to containers based on their labels. Its runtime image is
non-root, so it cannot open `/var/run/docker.sock` directly — it reaches the Docker API through a read-only
[socket proxy](https://github.com/Tecnativa/docker-socket-proxy) instead:

```yaml
services:
  dockerproxy:
    image: tecnativa/docker-socket-proxy
    environment:
      CONTAINERS: "1"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro

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
      - certs:/certs
    depends_on: [dockerproxy]

volumes:
  certs:
```

```bash
docker compose up -d
```

## Expose a container

Add nginx-proxy-compatible labels to any container. DockYARP picks it up live — no restart needed.

```yaml
services:
  web:
    image: my-app
    labels:
      - "VIRTUAL_HOST=app.example.io"
      - "VIRTUAL_PORT=8080"
```

The container is now served at `http://app.example.io/`.

## Add automatic TLS

Declare `LETSENCRYPT_HOST` (and a contact email) to have a certificate provisioned and renewed for the host:

```yaml
    labels:
      - "VIRTUAL_HOST=app.example.io"
      - "VIRTUAL_PORT=8080"
      - "LETSENCRYPT_HOST=app.example.io"
      - "LETSENCRYPT_EMAIL=admin@example.io"
```

{{% alert title="Tip" color="primary" %}}
Labels are read live — DockYARP does not need a restart when a container starts, stops, or changes labels.
{{% /alert %}}

## Next steps

- [Configuration](/docs/configuration/) — the full label and application-config reference.
- [Features](/docs/features/) — what DockYARP does at runtime (discovery, routing, TLS, admin API…).
- [Examples](/docs/examples/) — copy-pasteable recipes for common setups.
- [Architecture](/docs/architecture/) — how discovery, routing (YARP), and TLS fit together.
- [Deployment](/docs/deployment/) — running DockYARP in production.
