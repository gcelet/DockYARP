## Context

Discovery lists running containers (`All=false`) and maps every one to an endpoint; container health is
never read. `DockerContainerSource.TryMapEvent` maps only `start`/`stop`/`die`/`update`, so `health_status`
transitions do not reconcile. The list response already carries the health in its `Status` string
(`Up 2 minutes (healthy)`), so no extra inspect call is needed.

## Goals / Non-Goals

**Goals:** model container health, exclude `unhealthy`/`starting` containers from routing (keeping healthy
siblings), and reconcile on `health_status` events. Pure parsing is unit-testable without a Docker daemon.

**Non-Goals:** active proxy-side health probing (YARP health checks already modeled separately), per-label
health-check tuning, waiting/backoff policies beyond Docker's own health state.

## Decisions

- **`ContainerHealth` enum** (`None`, `Starting`, `Healthy`, `Unhealthy`) in `DockYarp.Docker.Models`;
  `ContainerInfo.Health` defaults to `None` (no health check ⇒ always routed).
- **Pure parsers in a testable helper** `ContainerStatusParser`:
  - `ParseHealth(status)` reads the Docker status string (`(healthy)`, `(unhealthy)`, `(health: starting)`),
    defaulting to `None`.
  - `MapAction(action)` maps a Docker event action to a `ContainerEventKind?`, treating any
    `health_status*` action as `Updated`. `DockerContainerSource` delegates to both, so the daemon-facing
    code stays a thin adapter and the logic is covered by unit tests.
- **Exclusion in `ContainerMapper`**: after a container parses to a valid config, if its health is
  `Unhealthy` or `Starting` it is skipped with a warning and contributes no endpoint. Because exclusion is
  per container, healthy siblings still populate the host's cluster; a host whose containers are all
  excluded yields no route (rather than an empty-cluster 503).

## Risks / Trade-offs

- Parsing the status string (vs. inspecting each container) is cheap and matches how `docker ps` surfaces
  health; if Docker changes the text format the parser degrades to `None` (fail-open to routed), which is
  the same as today's behavior — acceptable.
- A `starting` container is excluded until healthy; the `health_status` reconcile picks it up on transition.

## Migration Plan

Additive: one enum + one optional model field (default `None`) + parsing/exclusion wiring. Behavior is
unchanged for containers without a health check.

## Open Questions

- Surfacing excluded/unhealthy counts on `/api/health` — deferred (belongs with a later observability pass).
