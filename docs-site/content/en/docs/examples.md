---
title: Examples
weight: 4
description: Copy-pasteable recipes for common DockYARP setups.
---

Task-oriented recipes. Each builds on the **base stack** below and uses real labels/environment variables — see
[Configuration](../configuration/) for every key and [Features](../features/) for the behavior.

## Base stack

DockYARP behind a read-only Docker socket proxy (so the proxy stays non-root). Add the backend services from the
recipes below to this `docker-compose.yml`.

```yaml
services:
  dockerproxy:
    image: tecnativa/docker-socket-proxy
    environment:
      CONTAINERS: "1"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro

  dockyarp:
    image: dockyarp:local            # or your published image
    ports:
      - "80:8080"
      - "443:8443"
    environment:
      Docker__Enabled: "true"
      Docker__DockerEndpoint: "tcp://dockerproxy:2375"
      AdminApi__ApiKey: "change-me"
    volumes:
      - certs:/certs
      - ./config:/config
    depends_on: [dockerproxy]

volumes:
  certs:
```

## Basic virtual host

```yaml
  web:
    image: traefik/whoami
    labels:
      VIRTUAL_HOST: "app.local"
      VIRTUAL_PORT: "80"
```

`http://app.local/` reaches `web`. Use a comma-separated list (`app.local,www.app.local`) to expose one
container under several names.

## Path routing with a rewrite

```yaml
  api:
    image: my/api
    labels:
      VIRTUAL_HOST: "app.local"
      VIRTUAL_PORT: "8080"
      VIRTUAL_PATH: "/api"
      VIRTUAL_DEST: "/"
```

`http://app.local/api/orders` is forwarded to `api` as `/orders` (the `/api` prefix is stripped).

{{% alert title="Heads-up: /api and the Admin API" color="warning" %}}
DockYARP's Admin API and `/metrics` currently serve a few **exact** paths on **all** hosts, taking precedence over
proxying: `/api/version`, `/api/routes`, `/api/clusters`, `/api/certs`, `/api/resolve`, `/api/health`, and `/metrics`.
A backend that exposes one of these **exact** paths (notably the very common `/api/health`) is **shadowed** — the
request is answered by DockYARP (a `401` without an API key) instead of being proxied. Other paths such as
`/api/orders` proxy normally. Isolating the admin interface behind a dedicated host/port is planned.
{{% /alert %}}

## Multiple ports on one container

```yaml
  app:
    image: my/app
    labels:
      VIRTUAL_HOST_MULTIPORTS: "{app.local: {/: {port: 8080}, /admin: {port: 9090}}}"
```

`/` routes to port 8080 and `/admin` to port 9090 on the same container.

## Configure via environment variables

The `VIRTUAL_*` keys work as environment variables too (and an env var wins over a same-named label):

```yaml
  web:
    image: traefik/whoami
    environment:
      VIRTUAL_HOST: "app.local"
      VIRTUAL_PORT: "80"
```

## Automatic HTTPS (Let's Encrypt)

Point the proxy at the ACME production directory and accept the terms, then label the backend:

```yaml
  dockyarp:
    environment:
      Tls__AcmeDirectoryUri: "https://acme-v02.api.letsencrypt.org/directory"
      Tls__AcceptTermsOfService: "true"
      Tls__ContactEmail: "admin@example.com"

  web:
    image: my/web
    labels:
      VIRTUAL_HOST: "app.example.com"
      VIRTUAL_PORT: "8080"
      LETSENCRYPT_HOST: "app.example.com"
```

DockYARP obtains and renews a certificate for `app.example.com` (needs public DNS and a reachable port 80), and
redirects HTTP to HTTPS once the certificate exists.

## Mutual TLS (client certificates)

```yaml
  dockyarp:
    environment:
      Tls__ClientCaCertificatePath: "/certs/client-ca.pem"   # mount your client CA here

  api:
    image: my/api
    labels:
      VIRTUAL_HOST: "api.example.com"
      VIRTUAL_PORT: "8080"
      LETSENCRYPT_HOST: "api.example.com"
      DOCKYARP_CLIENT_CERT: "required"
```

Requests to `api.example.com` must present a client certificate chaining to the configured CA.

## Per-host TLS policy

```yaml
  web:
    image: my/web
    labels:
      VIRTUAL_HOST: "modern.example.com"
      VIRTUAL_PORT: "8080"
      LETSENCRYPT_HOST: "modern.example.com"
      SSL_POLICY: "Mozilla-Modern"
```

`modern.example.com` negotiates only TLS 1.3; other hosts keep the global posture.

## Basic Auth

```yaml
  admin:
    image: my/admin
    labels:
      VIRTUAL_HOST: "admin.local"
      VIRTUAL_PORT: "8080"
      DOCKYARP_AUTH_USER: "alice"
      DOCKYARP_AUTH_PASSWORD: "s3cret"
```

Requests without valid credentials get `401` with a `WWW-Authenticate: Basic` challenge.

## Basic Auth from htpasswd files

Mount a directory of Apache htpasswd files and point DockYARP at it. A file named `<host>` protects that host
(bcrypt/apr1/SHA1 hashes; reloaded live):

```yaml
  dockyarp:
    environment:
      Security__HtpasswdDirectory: "/auth"
    volumes:
      - ./auth:/auth:ro            # e.g. a file named "admin.local"

  admin:
    image: my/admin
    labels:
      VIRTUAL_HOST: "admin.local"
      VIRTUAL_PORT: "8080"
```

Any user listed in `/auth/admin.local` can authenticate; add `admin.local_<sha1(path)>` to protect a single path.

## Internal-only route

```yaml
  internal:
    image: my/internal
    labels:
      VIRTUAL_HOST: "internal.local"
      VIRTUAL_PORT: "8080"
      NETWORK_ACCESS: "internal"
```

Clients outside the internal ranges (`Security:InternalRanges`) get `403`.

## Behind a load balancer

When an upstream load balancer terminates on non-standard ports and forwards `X-Forwarded-*`:

```yaml
  dockyarp:
    environment:
      Proxy__TrustDownstreamProxy: "true"

  web:
    image: my/web
    labels:
      VIRTUAL_HOST: "app.example.com"
      VIRTUAL_PORT: "8080"
      LETSENCRYPT_HOST: "app.example.com"
      EXTERNAL_HTTPS_PORT: "8443"
```

DockYARP trusts the forwarded headers and builds HTTP→HTTPS redirects using `:8443`.
