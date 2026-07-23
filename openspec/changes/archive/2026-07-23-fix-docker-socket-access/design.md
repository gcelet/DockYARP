## Context

`DockerContainerSource` builds its client from `Docker__DockerEndpoint` (an optional URI; when unset it uses
the platform default, i.e. the local Unix socket). The chiseled runtime runs as a non-root user, which cannot
read a `root:docker 660` socket — hence `SocketException (13): Permission denied`. The fix is about *how the
non-root container reaches the API*, not about the discovery code.

## Goals / Non-Goals

**Goals:** make discovery work with a non-root DockYarp; a secure, portable default; keep a lightweight
alternative; apply consistently to the reference stack and the e2e harness.

**Non-Goals:** running DockYarp as root; changing the discovery/client code; a production hardening guide
beyond the two documented modes.

## Decisions

- **Default: `tecnativa/docker-socket-proxy`.** It mounts the socket (running privileged is acceptable for a
  minimal, audited, read-only API gateway) and exposes the Docker API over `tcp://dockerproxy:2375`. Only the
  endpoints discovery needs are enabled — `CONTAINERS=1` (list/inspect, the call that failed) with the proxy's
  default `EVENTS=1` (the event watch). DockYarp sets `Docker__DockerEndpoint=tcp://dockerproxy:2375`, drops
  the socket mount, and stays non-root. Portable: no dependency on the host's `docker` GID.
- **Alternative: `group_add`.** For operators who prefer the direct socket, `examples/docker-compose.group-add.yml`
  mounts the socket read-only and adds the container to the socket's owning group via `group_add: ["${DOCKER_GID}"]`,
  where `DOCKER_GID` is `stat -c '%g' /var/run/docker.sock`. Non-root is preserved; the trade-off is the
  host-specific GID.
- **e2e harness uses the default mode.** The Aspire AppHost adds a `dockerproxy` container, points DockYarp
  at it, drops the socket mount, and `WaitFor(dockerproxy)` — so the tests exercise the recommended path.
- **No code change.** The default `DockerEndpoint` stays null (local default) so nothing changes for a root
  deployment; both modes are pure configuration.

## Risks / Trade-offs

- **Socket proxy scope**: enabling too little breaks discovery, too much widens the attack surface. Start at
  `CONTAINERS=1` (+ default `EVENTS`); widen only if discovery needs more (validated at runtime).
- **Network resolution**: DockYarp must resolve `dockerproxy` on the shared network — the same assumption
  the e2e already relies on.
- **group_add portability**: the GID differs per host; documented, and it is the non-default path.

## Migration Plan

Config/infra only. Existing root-based deployments (unset `DockerEndpoint`, socket mounted) keep working. The
reference stack and e2e move to the proxy; the group-add example is provided for the alternative.

## Open Questions

- Whether discovery needs any proxy permission beyond `CONTAINERS` (e.g. `NETWORKS`) — confirmed at runtime
  against the reference stack.
