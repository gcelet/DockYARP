## Why

The first real run of the reference stack against a live Docker daemon (the Nuke `Smoke` target) failed:
DockYarp never proxied the sample service. The logs show the discovery worker cannot reach the Docker
API — `System.Net.Sockets.SocketException (13): Permission denied` connecting to `/var/run/docker.sock`.

DockYarp's runtime image is **chiseled and non-root** (by design), but the mounted Docker socket is owned
`root:docker` with mode `660`, so a non-root process cannot read it. nginx-proxy avoids this by running as
root; DockYarp must not. Discovery therefore fails, no routes are built, and every request 404s. The same
socket mount is used by the Aspire end-to-end AppHost, so it hits the identical failure.

## What Changes

No product code change is needed — `DockerContainerSource` already honours `Docker__DockerEndpoint` (any URI,
including `tcp://`). We change how the **non-root** container reaches the Docker API and document both modes:

- **Default (recommended): a Docker socket proxy.** Add a `tecnativa/docker-socket-proxy` service that mounts
  the socket (it handles the privileged access) and exposes a read-only Docker API over TCP. DockYarp sets
  `Docker__DockerEndpoint=tcp://dockerproxy:2375`, mounts **no** socket, and stays non-root. Applied to the
  reference `docker-compose.yml`, the Aspire e2e AppHost, examples, and docs.
- **Alternative (supported): group membership.** A committed example `examples/docker-compose.group-add.yml`
  keeps the direct socket mount and adds the container to the socket's owning group
  (`group_add: ["${DOCKER_GID}"]`), documented with how to obtain the host GID.

## Capabilities

### Modified Capabilities
- `deployment`: the reference stack accesses the Docker API as a non-root container via a socket proxy by
  default, with a documented group-membership alternative.

## Impact

- **Config / infra only**: `docker-compose.yml`, new `examples/docker-compose.group-add.yml`,
  `tests/DockYarp.E2E.AppHost` (socket proxy instead of the socket mount), `docs/deployment.md`. No `src/`
  changes.
- **Fixes**: the `Smoke` target and unblocks the Aspire e2e discovery (same root cause).
- **Owning agent**: AG-DEP (with AG-DD).
