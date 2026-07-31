---
id: add-docker-daemon-tls
capability: docker-discovery
agent: AG-DD
tier: B-runtime
priority: low
status: backlog
nginx-proxy: DOCKER_HOST / DOCKER_TLS_VERIFY / DOCKER_CERT_PATH (docker-gen)
provenance: 2026-07-31 parity re-analysis
---

## Why
docker-gen can talk to a **remote** Docker daemon over TLS (`DOCKER_HOST=tcp://…` + `DOCKER_TLS_VERIFY` +
`DOCKER_CERT_PATH`). DockYarp configures only the endpoint URI; it cannot verify/authenticate a TLS daemon
connection, so remote-daemon setups over TLS are unsupported.

## nginx-proxy behavior
- docker-gen: `-endpoint`/`DOCKER_HOST` (default `unix:///var/run/docker.sock`), `-tlsverify`/`DOCKER_TLS_VERIFY`,
  `-tlscert`/`-tlskey`/`-tlscacert` (default dir `DOCKER_CERT_PATH` or `~/.docker`); a TLS client is used for
  non-unix endpoints.

## DockYarp today
- `DockerDiscoveryOptions.DockerEndpoint` selects the endpoint; `DockerContainerSource.CreateClient` builds a
  `DockerClientConfiguration` from the URI only — no client-cert / CA / verify options.

## Proposed change (sketch)
- Add `Docker` TLS options (CA path, client cert/key, verify) and pass appropriate credentials to
  `DockerClientConfiguration` for `tcp://` endpoints. Socket endpoints unaffected.

## Acceptance criteria (→ scenarios)
- **WHEN** a `tcp://` endpoint with TLS options is configured **THEN** discovery connects using the client
  certificate and verifies the daemon per the configured CA.
- **WHEN** a unix-socket endpoint is used **THEN** behavior is unchanged.

## Notes / risks / references
- Mostly relevant to remote/rootless-over-TCP setups; the socket path (default) is unaffected. Validate live
  against a TLS-enabled daemon (Docker-capable session).
