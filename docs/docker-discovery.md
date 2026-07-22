# Docker discovery (DockYarp.Docker)

DockYarp discovers containers from the Docker daemon and keeps the routing store in sync. See
[labels-reference.md](labels-reference.md) for the labels it reads and [routing-model.md](routing-model.md)
for what it writes into.

## Components

| Type | Role |
|---|---|
| `IContainerSource` | Abstraction over the daemon: list running containers + stream lifecycle events. |
| `DockerContainerSource` | Docker.DotNet-backed implementation (unix socket / named pipe). |
| `LabelParser` | Pure parser: container labels → `ContainerLabelConfig`. |
| `ContainerMapper` | Maps containers → a dynamic `ConfigContribution` (routes/clusters/endpoints/TLS). |
| `DiscoveryReconciler` | List → map → merge → publish to `IRouteConfigStore`. |
| `DockerDiscoveryService` | Hosted service: startup reconcile, event loop, reconnect with backoff. |

## Reconciliation model

Startup and every event/reconnect run the **same full reconciliation**: enumerate running containers,
map them, merge (resolving precedence, reporting diagnostics), and publish. Events act as triggers; the
authoritative state always comes from a full enumeration. This single code path guarantees convergence and
covers containers started before DockYarp, replicas joining/leaving, and changes missed during an outage.

## Health-aware selection

Routing follows Docker health: a container that is **unhealthy** or still **starting** is excluded (its
endpoint is not added) and the exclusion is logged; a container that is **healthy** or declares **no health
check** is routed. Exclusion is per container, so a healthy replica keeps serving a host when a sibling is
unhealthy. Health is read from the container status (no extra inspect call), and `health_status` events
trigger a reconciliation so a container is picked up as soon as it becomes healthy (and dropped when it
turns unhealthy).

## Event handling and failure modes

- **Events handled**: `start`, `stop`, `die`, `update`, and `health_status` (container scope). Each triggers
  a reconciliation.
- **Startup reconciliation**: running containers are enumerated at startup, so a container started before
  DockYarp is routed without waiting for an event.
- **Daemon restart / connection drop**: the event stream ends or throws. The service reconnects with
  **capped exponential backoff** (`InitialReconnectDelay` → `MaxReconnectDelay`) and, on reconnect,
  reconciles so any changes during the outage converge.
- **Resilience**: failures talking to Docker never crash the host — they are logged and retried. A single
  container with invalid labels is skipped and logged; other containers keep working.
- **Ambiguous port**: a container exposing several ports without `VIRTUAL_PORT` is skipped with a warning.

## Network address selection

A container on several networks has several IPs; only the IP on a network the proxy shares is reachable.
DockYarp selects the forwarded address as follows: if `PreferredNetwork` is configured and the container
is attached to it, that network's IP is used; otherwise it picks deterministically (ordinal by network
name), **skipping** the Swarm `ingress` network; if no network address is available it falls back to the
container name (resolvable on a shared network). Set `PreferredNetwork` to the network the proxy shares with
its backends.

## Configuration

`DockerDiscoveryOptions`:

- `DockerEndpoint` — daemon URI; `null` uses the platform default
  (`unix:///var/run/docker.sock` on Linux, `npipe://./pipe/docker_engine` on Windows).
- `PreferredNetwork` — Docker network whose IP is preferred when a container is attached to it; `null`
  selects deterministically (ingress skipped).
- `InitialReconnectDelay` / `MaxReconnectDelay` — reconnect backoff bounds.

Register with `services.AddDockerDiscovery(options)` (the host must also register an `IRouteConfigStore`).
