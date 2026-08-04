# Design — add-doc-examples (recipes page)

## Scope
One **Examples** page of task-oriented recipes. Each recipe is a small, real Compose fragment (backend service +
its labels/env) on top of a **base stack shown once** — the `dockyarp` + `dockerproxy` services from the repo's
`docker-compose.yml` — plus one or two lines on the expected result. Concise, not exhaustive.

## Base stack (from the repo `docker-compose.yml`, verified)
- `dockerproxy` (tecnativa/docker-socket-proxy, `CONTAINERS=1`, socket mounted read-only) — the only component
  touching the socket, so `dockyarp` stays non-root.
- `dockyarp` — ports `80:8080` / `443:8443`, `Docker__Enabled=true`,
  `Docker__DockerEndpoint=tcp://dockerproxy:2375`, `AdminApi__ApiKey`, volumes `certs:/certs` + `./config:/config`.

## Recipes (real labels/env from the reference)
1. Basic virtual host (`VIRTUAL_HOST`/`VIRTUAL_PORT`). 2. Path routing + rewrite (`VIRTUAL_PATH`/`VIRTUAL_DEST`).
3. Multiple ports (`VIRTUAL_HOST_MULTIPORTS`). 4. Configure via environment variables (VIRTUAL_* under
`environment:`). 5. Automatic HTTPS (`LETSENCRYPT_HOST`/`_EMAIL` + `Tls__AcmeDirectoryUri` production +
`Tls__AcceptTermsOfService`). 6. Mutual TLS (`Tls__ClientCaCertificatePath` + `DOCKYARP_CLIENT_CERT=required`).
7. Per-host TLS policy (`SSL_POLICY=Mozilla-Modern`). 8. Basic Auth (`DOCKYARP_AUTH_USER`/`_PASSWORD`).
9. Internal-only (`NETWORK_ACCESS=internal`). 10. Behind a load balancer (`Proxy__TrustDownstreamProxy` +
`EXTERNAL_HTTPS_PORT`).

## Placement
`examples.md` weight after Features; bump architecture/deployment/contributing accordingly. Cross-link from
Getting Started / Configuration / Features.
