## Why
nginx-proxy (via docker-gen's `-container-filter` / `DOCKER_CONTAINER_FILTERS`) can restrict which containers
are considered using native Docker filters (e.g. `label=…`, `network=…`, `name=…`). DockYarp discovers every
container carrying `VIRTUAL_HOST`, with no way to scope discovery to a subset — needed for multi-proxy hosts or
noisy daemons where only a labelled subset should be routed by this instance.

## What Changes
- Add a `Docker:ContainerFilters` option: a map of Docker filter key to one or more values (AND across keys, OR
  within a key — Docker's native filter semantics).
- Apply the filter to the **authoritative container listing** used by every reconciliation pass, so only
  matching containers are ever discovered/routed. When no filter is configured, discovery is unchanged.
- The event stream stays a generic change-trigger (each event reconciles against the filtered listing); it is
  not itself filtered — see `design.md` for why (docker-gen precedent + `network=` event-scope ambiguity).

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `docker-discovery`: discovery can be scoped to a subset of containers via Docker-native inclusion filters.

## Impact
- **Code**: `DockYarp.Docker` — `DockerDiscoveryOptions.ContainerFilters`, a pure `DockerFilters.Build`
  helper (option map → Docker `IDictionary<string, IDictionary<string, bool>>`), and `DockerContainerSource`
  passing the built filter to `ListContainersAsync`.
- **Tests**: `DockerFilters.Build` (empty → none; single key/value; multi-value OR; multi-key AND shape; a
  value containing `=` preserved). `DockerContainerSource` wiring is covered by inspection — it wraps the live
  daemon and has no existing unit tests.
- **Config**: bound from the existing `Docker` section, e.g.
  `Docker:ContainerFilters:label:0 = dockyarp.enable=true`.
- **Owning agent**: AG-DD. Resolves `add-docker-container-filters`.
