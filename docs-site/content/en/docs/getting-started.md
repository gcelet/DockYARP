---
title: Getting started
weight: 1
description: Run DockYARP and expose your first container.
---

## Run the proxy

DockYARP watches the Docker socket and routes to containers based on their labels.

```bash
docker run -d --name dockyarp \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -p 80:80 -p 443:443 \
  dockyarp
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
- [Architecture](/docs/architecture/) — how discovery, routing (YARP), and TLS fit together.
- [Deployment](/docs/deployment/) — running DockYARP in production.
