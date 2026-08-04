## Why
docker-gen can talk to a **remote** Docker daemon over TLS (`DOCKER_HOST=tcp://…` + `DOCKER_TLS_VERIFY` +
`DOCKER_CERT_PATH`). DockYarp configures only the endpoint URI (`DockerContainerSource.CreateClient` builds a
`DockerClientConfiguration` from the URI alone), so it cannot present a client certificate or verify a TLS
daemon — remote-daemon-over-TLS setups are unsupported.

## What Changes
- Add `Docker:CertPath` (a directory holding `ca.pem`, `cert.pem`, `key.pem` — the Docker `DOCKER_CERT_PATH`
  convention) and `Docker:TlsVerify` (bool) options.
- For a `tcp://` endpoint with `CertPath` set, DockYarp connects using the client certificate (`cert.pem` +
  `key.pem`) and, when `TlsVerify` is `true`, validates the daemon's certificate against `ca.pem` (custom root
  trust, offline). A unix-socket / `npipe` endpoint, or no `CertPath`, is unaffected (default connection).

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `docker-discovery`: the daemon connection can present a client certificate and verify the daemon over TLS.

## Impact
- **Code**: `DockYarp.Docker` — `DockerDiscoveryOptions.CertPath`/`TlsVerify`; new `DockerTlsCredentials`
  (builds a `Docker.DotNet` `Credentials` from PEM strings + a server-validation callback);
  `DockerContainerSource.CreateClient` reads the PEM files and passes credentials to
  `DockerClientConfiguration`.
- **Design note**: the pinned Docker.DotNet 3.125.15 has **no** `CertificateCredentials` type (only
  `AnonymousCredentials` + the abstract `Credentials`), so this change implements a small custom `Credentials`
  subclass that wires the client certificate + validation callback onto Docker.DotNet's public
  `ManagedHandler`. See `design.md`.
- **Tests (unit)**: `DockerTlsCredentials` — null for a non-TLS endpoint / missing client cert; a TLS endpoint
  yields TLS credentials that wire a `ManagedHandler`'s client certificate + server-validation callback; the
  callback accepts a daemon cert chaining to the configured CA and rejects one that does not.
- **Docs**: the site configuration reference documents `Docker:CertPath` / `Docker:TlsVerify`.
- **Runtime / e2e**: the live connection to a TLS daemon is runtime (not unit-tested); credential construction
  and validation are unit-tested on Windows.
- **Owning agent**: AG-DD. Resolves `add-docker-daemon-tls`.
