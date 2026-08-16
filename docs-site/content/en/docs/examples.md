---
title: Examples
weight: 4
description: Copy-pasteable recipes for common DockYARP setups.
---

Task-oriented recipes. Each builds on the **base stack** below and uses real labels/environment variables — see
[Configuration](../configuration/) for every key and [Features](../features/) for the behavior.

## Base stack

DockYARP's runtime image is non-root and cannot open `/var/run/docker.sock` directly — a read-only Docker
socket proxy is **required**, not optional hardening, for it to reach the Docker API at all. Add the backend
services from the recipes below to this `docker-compose.yml`.

```yaml
services:
  dockerproxy:
    image: tecnativa/docker-socket-proxy
    environment:
      CONTAINERS: "1"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro

  dockyarp:
    image: gcelet/dockyarp           # or dockyarp:local for a local build
    ports:
      - "80:8080"
      - "443:8443"
    environment:
      Docker__Enabled: "true"
      Docker__DockerEndpoint: "tcp://dockerproxy:2375"
    volumes:
      - certs:/certs
      - ./config:/config
    depends_on: [dockerproxy]

volumes:
  certs:
```

The admin API and dashboard are off by default (`AdminApi:Surface: Disabled`) — see
[Dedicated admin host](#dedicated-admin-host-with-its-own-https-certificate) below to turn them on.

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

{{% alert title="Heads-up: /api, /dashboard, and the Admin API" color="warning" %}}
The admin surface — the JSON API (`/api/version`, `/api/routes`, `/api/clusters`, `/api/certs`, `/api/resolve`,
`/api/health`), the read-only dashboard (`/dashboard`), and `/metrics` — is **off by default**
(`AdminApi:Surface: Disabled`), so none of those exact paths are intercepted and a backend that happens to own
one of them (notably the very common `/api/health`) is never shadowed. Turning it on (`AdminApi:Surface: Api` or
`ApiAndDashboard`) **requires `AdminApi:Host` to be set** — DockYARP refuses to start otherwise — so once
enabled, those paths answer only on the dedicated host and are proxied normally everywhere else. (Other paths
such as `/api/orders` always proxy, regardless of `Surface`.) The dashboard has **no application-level
authentication** of its own — unlike `/api/*`, which requires the `X-Api-Key` header — so only opt into
`ApiAndDashboard` on a host you keep off the public internet.
{{% /alert %}}

## Dedicated admin host (with its own HTTPS certificate)

Turn on the admin API + `/metrics`, scoped to a dedicated host, and let DockYARP obtain an ACME certificate for
that host too:

```yaml
  dockyarp:
    environment:
      AdminApi__ApiKey: "change-me"
      AdminApi__Surface: "Api"                   # "ApiAndDashboard" also serves /dashboard
      AdminApi__Host: "admin.example.com"        # required whenever Surface isn't Disabled
      AdminApi__LetsEncrypt: "true"              # provision a real certificate for the admin host
      AdminApi__ContactEmail: "admin@example.com"  # optional; falls back to Tls__ContactEmail
      Tls__AcmeDirectoryUri: "https://acme-v02.api.letsencrypt.org/directory"
      Tls__AcceptTermsOfService: "true"
```

`https://admin.example.com/api/health` reaches the admin API (behind the `X-Api-Key`), served with an ACME
certificate; on every application host the same paths are proxied to the backend. The admin host needs public DNS
and a reachable port 80 for the HTTP-01 challenge, like any other certified host. Leave `AdminApi__LetsEncrypt`
unset (or `false`) to keep the self-signed/operator certificate on the admin host.

## No host port-remap (macvlan, host networking)

ACME HTTP-01 needs port 80 reachable from the certificate authority, and clients need port 443 reachable,
regardless of topology. The base stack above works because Docker's own host port-remap silently maps published
host ports 80/443 onto the container's non-root listen ports 8080/8443. A topology with **no such remap** —
macvlan (the container gets its own LAN-routable interface) or host networking — has no port-remap layer at
all, so the container must listen on 80/443 itself:

```yaml
  dockyarp:
    image: gcelet/dockyarp   # or dockyarp:local for a local build
    cap_add:
      - NET_BIND_SERVICE   # lets the non-root process bind ports 80/443 directly
    environment:
      Docker__Enabled: "true"
      Docker__DockerEndpoint: "tcp://dockerproxy:2375"
      Server__HttpPort: "80"
      Server__HttpsPort: "443"
    volumes:
      - certs:/certs
      - ./config:/config
    depends_on: [dockerproxy]
    network_mode: "host"   # or your macvlan network, with no `ports:` block either way
```

`NET_BIND_SERVICE` is the standard Linux capability for "a non-root process needs a privileged port" — the same
pattern nginx's own official image uses via `setcap`, not a reason to run as root. No `ports:` mapping is used
or needed: the container is directly reachable on 80/443 through its own network interface.

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
