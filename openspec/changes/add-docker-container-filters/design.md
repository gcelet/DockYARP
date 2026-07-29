# Design — add-docker-container-filters

## Context
Discovery (`DockerContainerSource`) both lists running containers (`ListContainersAsync`) and watches the
event stream (`MonitorEventsAsync`). Each lifecycle event triggers a full `DiscoveryReconciler.ReconcileAsync`,
which **re-lists** and rebuilds the configuration. The listing is therefore the single source of truth; events
are only "something changed, re-list" pokes. We add Docker-native inclusion filters to scope which containers
discovery ever sees.

## Decisions

### 1. Config shape: a map of Docker filter key → values
`Docker:ContainerFilters` binds to `IDictionary<string, IReadOnlyList<string>>`. Keys are Docker filter keys
(`label`, `name`, `network`, …); each key lists its accepted values. This mirrors Docker's own filter model:
values within one key are OR-ed, distinct keys are AND-ed. Binding example (ASP.NET configuration):

```
Docker:ContainerFilters:label:0 = dockyarp.enable=true
Docker:ContainerFilters:network:0 = edge
```

We keep the map shape rather than parsing nginx-proxy's comma-separated string: it binds natively from any
configuration provider and needs no bespoke parser. (A `key=value` label filter is expressed as
`label` → `dockyarp.enable=true`, matching Docker's own encoding.)

### 2. A pure `DockerFilters.Build` helper
Converting the option map into Docker.DotNet's `IDictionary<string, IDictionary<string, bool>>` (the shape both
`ContainersListParameters.Filters` and the events API expect, where the inner `bool` is "include = true") is
the only non-trivial logic, so it lives in a static, side-effect-free `DockerFilters.Build` that is unit
tested. Empty/whitespace keys and values are dropped; an empty result returns `null` so callers pass no filter.

### 3. Apply to the listing only; leave the event stream generic
The filter is applied to `ListContainersAsync` (the authoritative set every reconcile reads). The event stream
is **not** filtered.

- **Precedent**: docker-gen passes `-container-filter` to the container *list* API only; it watches all events
  and regenerates. We match that.
- **Correctness**: Docker's events endpoint interprets some filter keys differently from the list endpoint.
  `network=` on events matches network-scoped events, not "container attached to this network" — filtering the
  stream by it could **drop** a relevant container `start`/`stop` event and miss a change. Filtering only the
  authoritative listing cannot cause wrong routing: a filtered-out container is simply absent from every
  reconcile, so no route is created or removed for it, regardless of which event poked the reconcile.

This fully satisfies the acceptance criteria: only matching containers are routed (listing is filtered), a
filtered-out container's events produce no routing change (it is absent from the filtered listing), and no
filter preserves today's behavior.

### 4. Testability
The pure `DockerFilters.Build` is unit tested. `DockerContainerSource` builds the filter once (constructor) and
passes it to `ListContainersAsync`; that wiring is verified by inspection, consistent with the type having no
existing unit tests because it wraps the live Docker daemon.

## Risks
- A malformed filter key/value silently narrows discovery. Mitigated by dropping only empty entries and keeping
  the mapping faithful; operators see the effect via the existing reconcile logging (routed container count).
