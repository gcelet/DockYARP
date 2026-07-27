---
id: add-docker-container-filters
capability: docker-discovery
agent: AG-DD
tier: A-structural
priority: medium
status: backlog
nginx-proxy: DOCKER_CONTAINER_FILTERS (docker-gen -container-filter)
provenance: this parity pass (untracked feature)
---

## Why
nginx-proxy (via docker-gen) can restrict which containers are considered using native Docker filters
(`DOCKER_CONTAINER_FILTERS`, e.g. `network=mynetwork`, `label=...`). DockYarp discovers all containers with
valid labels; there is no way to scope discovery to a subset — useful for multi-proxy or noisy hosts.

## nginx-proxy behavior
- `DOCKER_CONTAINER_FILTERS` / docker-gen `-container-filter` apply Docker-native inclusion filters
  (AND-combined) at the API level, limiting the watched/considered container set.

## DockYarp today
Discovery watches all container events and maps any container carrying `VIRTUAL_HOST`
(`src/DockYarp.Docker/Discovery/DockerDiscoveryService.cs`, `DockerContainerSource.cs`); options in
`DockerDiscoveryOptions.cs` cover endpoint + preferred network but no inclusion filter.

## Proposed change (sketch)
Add a `Docker:ContainerFilters` option (map of key→values) passed to the Docker.DotNet
`ContainersListParameters.Filters` and the event subscription filter, so both the initial reconcile and the
event stream honor it.

## Acceptance criteria (→ scenarios)
- **WHEN** `Docker:ContainerFilters` restricts to `label=dockyarp.enable=true` **THEN** only containers with
  that label are discovered/routed.
- **WHEN** a filtered-out container starts/stops **THEN** discovery ignores the event.
- **WHEN** no filter is configured **THEN** discovery behaves as today.

## Notes / risks / references
- Keep filters applied consistently to both the reconcile listing and the event subscription.
