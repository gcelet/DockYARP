---
title: Migrating from nginx-proxy
weight: 5
description: Move an existing nginx-proxy deployment to DockYARP, with a safe rollback path.
---

DockYARP is built as an nginx-proxy equivalent — most of what an nginx-proxy operator already knows carries
over directly. This page covers two migration paths and, separately, the two things operators worry about most:
your existing certificates, and being able to roll back if something goes wrong. For the exhaustive
feature-by-feature comparison, see the [parity matrix]({{< repo-file "openspec/backlog/parity.md" >}}) in the
repository rather than this page.

**Either path**: only the nginx-proxy stack itself is replaced. Every backend service keeps its existing
`VIRTUAL_HOST`/`LETSENCRYPT_HOST`-style labels or environment variables unchanged — DockYARP reads the same
keys (see [Configuration](../configuration/)) — so no other stack's configuration needs to change.

## Basic migration

A typical setup: `nginxproxy/nginx-proxy` + `nginxproxy/acme-companion`, public Let's Encrypt, `VIRTUAL_HOST`
declared as an environment variable on each backend container (the convention nginx-proxy's own docs use).

1. Stand up DockYARP alongside the existing nginx-proxy stack, using the [base stack](../examples/#base-stack)
   (a Docker socket proxy + the `dockyarp` service) — don't stop nginx-proxy yet.
2. Copy your certificates into DockYARP's certificate volume — see [Certificates and rollback](#certificates-and-rollback)
   below before doing anything else.
3. Your backend environment variables (or labels, if you use those instead) already work as-is: `VIRTUAL_HOST`,
   `VIRTUAL_PORT`, `LETSENCRYPT_HOST`, `LETSENCRYPT_EMAIL` are read unchanged (see
   [Configuration](../configuration/) for the full reference).
4. Point traffic (DNS, or your router/firewall's port forward) at DockYARP instead of nginx-proxy, and verify.
5. Keep the nginx-proxy stack stopped (not removed) for as long as you want the rollback option — see below.

### Worked example

nginx-proxy + `acme-companion`, exactly as [nginx-proxy's own two-container
example](https://github.com/nginx-proxy/acme-companion/blob/main/docs/Docker-Compose.md) documents it, plus one
backend container:

```yaml
services:
  nginx-proxy:
    image: nginxproxy/nginx-proxy
    container_name: nginx-proxy
    ports:
      - "80:80"
      - "443:443"
    labels:
      - "com.github.nginx-proxy.nginx"
    volumes:
      - certs:/etc/nginx/certs:ro
      - html:/usr/share/nginx/html
      - /var/run/docker.sock:/tmp/docker.sock:ro

  acme-companion:
    image: nginxproxy/acme-companion
    container_name: nginx-proxy-acme
    environment:
      - DEFAULT_EMAIL=admin@example.com
    volumes:
      - certs:/etc/nginx/certs:rw
      - html:/usr/share/nginx/html:rw
      - acme:/etc/acme.sh
      - /var/run/docker.sock:/var/run/docker.sock:ro

  app:
    image: my/app
    environment:
      - VIRTUAL_HOST=app.example.com
      - LETSENCRYPT_HOST=app.example.com

volumes:
  certs:
  html:
  acme:
```

The DockYARP equivalent — `nginx-proxy` and `acme-companion` are replaced by the [base stack](../examples/#base-stack);
`app` is **copied over unchanged**:

```yaml
services:
  dockerproxy:
    image: tecnativa/docker-socket-proxy
    environment:
      CONTAINERS: "1"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro

  dockyarp:
    image: ghcr.io/gcelet/dockyarp           # or dockyarp:local for a local build
    ports: ["80:8080", "443:8443"]
    environment:
      Docker__Enabled: "true"
      Docker__DockerEndpoint: "tcp://dockerproxy:2375"
      Tls__AcmeDirectoryUri: "https://acme-v02.api.letsencrypt.org/directory"
      Tls__AcceptTermsOfService: "true"
      Tls__ContactEmail: "admin@example.com"
    volumes:
      - certs:/certs            # see Certificates and rollback below before starting this for real
    depends_on: [dockerproxy]

  app:
    image: my/app
    environment:
      - VIRTUAL_HOST=app.example.com
      - LETSENCRYPT_HOST=app.example.com

volumes:
  certs:
```

The admin API and dashboard are off by default (`AdminApi:Surface: Disabled`) — see
[Examples](/docs/examples/#dedicated-admin-host-with-its-own-https-certificate) to turn them on behind a
dedicated host. Not required for the migration itself.

## Advanced migration

A more involved setup looks like this: the classic separate `nginx` + `docker-gen` + `acme-companion` trio
instead of the single `nginxproxy/nginx-proxy` image, a **private ACME certificate authority** instead of
public Let's Encrypt, and possibly more than one Docker network (for example, one network for services
reachable directly on your LAN, another for the ones reverse-proxied through nginx-proxy).

The three-container split exists mainly so the **official** `nginx` image can be used directly — rather than
depending on `nginxproxy/nginx-proxy`'s own release cadence for nginx security patches, `docker-gen` regenerates
the config for a separately-managed, official `nginx:alpine` (or any tag you choose to track) container.

The same concern applies to DockYARP itself, in reverse: it's a single bundled image (.NET on a
[chiseled](https://github.com/dotnet/dotnet-docker/blob/main/documentation/ubuntu-chiseled.md) Ubuntu base), so
its own patch cadence for .NET and Linux base-image CVEs matters just as much as nginx's did above. DockYARP
addresses this the same way any well-maintained base image should: Renovate pins the `Dockerfile`'s base image
to a digest and opens a PR the moment a new one is published; merging it rebuilds and republishes the image
automatically (`.github/workflows/base-image-refresh.yml`) — no manual tracking needed on either side.

Everything from the basic path applies. On top of that:

- **A global `SSL_POLICY`** (e.g. `Mozilla-Intermediate`) set on your `docker-gen` container maps directly to
  DockYARP's `Tls:SslPolicy` application setting — same preset names.
- **Multiple Docker networks**: DockYARP inspects its own container's networks on startup to determine which
  networks it can reach backends on (`Docker:ProxyNetworks`, auto-detected when unset) — no manual network list
  to maintain as you add backend networks.
- **A private ACME certificate authority**: see [Private ACME certificate authority](#private-acme-certificate-authority)
  below.

### Worked example

The `nginx` + `docker-gen` + `acme-companion` trio, exactly as [nginx-proxy's own three-container
example](https://github.com/nginx-proxy/acme-companion/blob/main/docs/Docker-Compose.md) documents it (the
`com.github.nginx-proxy.nginx`/`docker-gen` labels are how the containers find each other — easy to miss if
you're used to the single-container setup), plus a private CA and two backend stacks configured via
environment variables:

```yaml
# front-door stack — this is the one being replaced
services:
  nginx:
    image: nginx:alpine
    container_name: nginx-proxy
    ports:
      - "80:80"
      - "443:443"
    labels:
      - "com.github.nginx-proxy.nginx"
    volumes:
      - conf:/etc/nginx/conf.d:ro
      - html:/usr/share/nginx/html
      - certs:/etc/nginx/certs:ro

  docker-gen:
    image: nginxproxy/docker-gen
    container_name: nginx-proxy-gen
    command: -notify-sighup nginx-proxy -watch -wait 5s:30s /etc/docker-gen/templates/nginx.tmpl /etc/nginx/conf.d/default.conf
    labels:
      - "com.github.nginx-proxy.docker-gen"
    environment:
      - SSL_POLICY=Mozilla-Intermediate
    volumes:
      - conf:/etc/nginx/conf.d:rw
      - certs:/etc/nginx/certs:ro
      - /path/to/nginx.tmpl:/etc/docker-gen/templates/nginx.tmpl:ro
      - /var/run/docker.sock:/tmp/docker.sock:ro

  acme-companion:
    image: nginxproxy/acme-companion
    container_name: nginx-proxy-acme
    environment:
      - DEFAULT_EMAIL=admin@internal.example
      - ACME_CA_URI=https://ca.internal.example/acme/acme/directory
      - REQUESTS_CA_BUNDLE=/etc/certificates/private-root.crt
    volumes:
      - certs:/etc/nginx/certs:rw
      - html:/usr/share/nginx/html:rw
      - acme:/etc/acme.sh
      - /var/run/docker.sock:/var/run/docker.sock:ro

volumes:
  conf:
  html:
  certs:
  acme:
```

Two separate backend stacks, each its own `compose.yaml` — **untouched by the migration**:

```yaml
# api's own stack
services:
  api:
    image: my/api
    environment:
      - VIRTUAL_HOST=api.internal.example
      - LETSENCRYPT_HOST=api.internal.example
```

```yaml
# app's own stack
services:
  app:
    image: my/app
    environment:
      - VIRTUAL_HOST=app.internal.example
      - LETSENCRYPT_HOST=app.internal.example
```

The DockYARP equivalent replaces only the front-door stack — `api` and `app` above are **copied over unchanged**:

```yaml
services:
  dockerproxy:
    image: tecnativa/docker-socket-proxy
    environment:
      CONTAINERS: "1"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro

  dockyarp:
    image: ghcr.io/gcelet/dockyarp           # or dockyarp:local for a local build
    ports: ["80:8080", "443:8443"]
    environment:
      Docker__Enabled: "true"
      Docker__DockerEndpoint: "tcp://dockerproxy:2375"
      AdminApi__ApiKey: "change-me"
      Tls__SslPolicy: "Mozilla-Intermediate"
      Tls__AcmeDirectoryUri: "https://ca.internal.example/acme/acme/directory"
      Tls__AcceptTermsOfService: "true"
      Tls__ContactEmail: "admin@internal.example"
      SSL_CERT_FILE: "/etc/ssl/certs/combined-ca-bundle.pem"   # see Private ACME certificate authority below
    volumes:
      - certs:/certs
      - ./combined-ca-bundle.pem:/etc/ssl/certs/combined-ca-bundle.pem:ro
    depends_on: [dockerproxy]

volumes:
  certs:
```

`Docker:ProxyNetworks` is left unset here on purpose — DockYARP auto-detects its own reachable networks, so
adding a second network for LAN-direct services needs no config change on the DockYARP side either.

## Certificates and rollback

**Copy your certificates — never move them.** nginx-proxy (via `acme-companion`) stores certificates as
`<host>.crt` (the full chain) and `<host>.key` (the private key) in a flat directory. **Copy** that directory's
contents into DockYARP's certificate volume (`Tls:CertificateDirectory`, mounted at `/certs` in the
[base stack](../examples/#base-stack)) before DockYARP's first start.

No format conversion is needed: DockYARP accepts the exact same `<host>.crt`/`<host>.key` shape nginx-proxy and
acme-companion already produce (a full certificate chain in the `.crt` file, an RSA or EC private key in the
`.key` file). At startup, DockYARP detects a matching `<host>.crt`/`<host>.key` pair and **reuses it directly**
— it does not re-issue a certificate for a host that already has a valid one, only renewing when a certificate
is within its configured renewal window of expiring.

Because this is a copy, your original nginx-proxy installation's files are **never touched**. If anything looks
wrong after switching traffic to DockYARP, point traffic back at nginx-proxy and restart its stack — nothing
about the migration prevents that, at any point.

### Your ACME account (optional, but recommended against public Let's Encrypt)

DockYARP persists one ACME account per (contact email, ACME directory endpoint) pair and reuses it for every
certificate request and renewal, the same way `acme-companion` does — rather than starting fresh. If you skip
this step, DockYARP simply registers a **new** account on its first request; your certificates still work, but
you lose continuity with the account `acme-companion` already had reused across your existing certificates. Skipping
this is more of a concern against public Let's Encrypt (which applies per-account rate limits) than against a
private CA like `step-ca`.

To carry the existing account over: `acme-companion`'s underlying `acme.sh` client stores each account's key as
a PEM file (`account.key`) under its own persisted state, keyed by CA endpoint (and, on some installations, by
contact email). Locate it — `docker exec` into the `acme-companion` container and look under its ACME state
volume — and copy it to
`{CertificateDirectory}/acme/{LETSENCRYPT_EMAIL}/{acme-directory-host}/{acme-directory-path}/account.key`
(matching the certificate host you set `LETSENCRYPT_EMAIL` and `Tls:AcmeDirectoryUri` to) **before** DockYARP's
first request for that host. Check the key's algorithm first —
`openssl pkey -in account.key -noout -text | head -1` should show a 256-bit key (EC P-256); DockYARP only
supports importing an **EC** account key today, not RSA (`acme.sh`'s own default when no EC key length was
explicitly requested at registration). If your key is RSA, skip this step — DockYARP will register a new
account on first use.

## Private ACME certificate authority

If your certificates come from a private ACME certificate authority (for example a self-hosted `step-ca`
instance) rather than public Let's Encrypt, two things need pointing at it:

1. **The ACME directory itself** — set `Tls:AcmeDirectoryUri` to your CA's ACME directory URL (the same value
   used as `ACME_CA_URI` for `acme-companion`).
2. **Trusting the CA's own TLS certificate** — DockYARP's ACME client needs to trust your private CA to even
   reach its ACME directory over HTTPS (a separate concern from trusting the certificates DockYARP will request
   *from* it). DockYARP runs on .NET, which on Linux honors the standard OpenSSL `SSL_CERT_FILE` environment
   variable: mount a certificate bundle containing your private root CA (append it to a copy of the system's
   default CA bundle) into the DockYARP container, and set `SSL_CERT_FILE` to point at it.

   This was verified directly rather than assumed: a throwaway, unrelated self-signed CA was tested against
   DockYARP's exact container image on an isolated Docker network — the connection failed without
   `SSL_CERT_FILE` set (confirming no certificate is trusted by default) and succeeded once it was set.

A private CA reached via HTTP-01 challenge (the default for both `acme-companion` and DockYARP) needs no
further configuration beyond the two points above. DNS-01 (`DOCKYARP_ACME_CHALLENGE=dns-01`, required for a
wildcard `LETSENCRYPT_HOST`) is also supported — see [TLS & ACME](/docs/features/#tls--acme).

### DNS carried over from a split proxy/ACME setup

`acme-companion` and nginx itself are two separate containers with two separate network configurations —
`acme-companion` makes ACME calls from its own network, typically with ordinary Docker DNS (whatever the
Docker daemon forwards to). DockYARP is a single process doing both jobs, so it makes its ACME calls from the
exact same network/DNS configuration as the vhost it proxies. If nginx's own container carried a custom `dns:`
override (for example one needed only because it sits on a `macvlan` network without Docker's embedded DNS),
that override is now exercised for ACME calls too — and if it was never actually a working general-purpose
resolver (only ever reachable/needed for something narrower), ACME lookups for hosts outside that narrow case
will fail with a DNS resolution error, even though the same override worked fine for nginx. Point DockYARP's
`dns:` at a DNS server that can resolve **both** internal Docker names and your real domain names before
assuming a `Resource temporarily unavailable` provisioning failure is a network outage.
