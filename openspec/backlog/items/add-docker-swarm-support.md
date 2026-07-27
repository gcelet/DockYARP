---
id: add-docker-swarm-support
capability: docker-discovery
agent: AG-DD
tier: B-runtime
priority: low
status: backlog
nginx-proxy: Docker Swarm services
provenance: this parity pass (matrix: Swarm ⛔)
---

## Why
nginx-proxy (via docker-gen) can discover Docker **Swarm** services and their tasks, not just standalone
containers. DockYarp discovers standalone containers only, so Swarm deployments aren't supported.

## nginx-proxy behavior
- docker-gen exposes Swarm metadata (`SwarmNode`, service tasks) with deterministic address/network ordering
  to avoid spurious reloads; the template ignores the `ingress` network when selecting backend addresses.

## DockYarp today
Discovery watches container events via Docker.DotNet (`src/DockYarp.Docker/Discovery/DockerContainerSource.cs`)
and already skips the Swarm `ingress` network during address selection, but does not model Swarm services/tasks
(matrix ⛔).

## Proposed change (sketch)
Add a Swarm-aware discovery mode that lists services + tasks (Docker.DotNet Swarm APIs), maps service labels to
routes, and aggregates task endpoints into clusters with deterministic ordering. Gate behind a config flag;
keep standalone-container discovery as the default.

## Acceptance criteria (→ scenarios)
- **WHEN** Swarm mode is enabled and a service carries `VIRTUAL_HOST` **THEN** its running tasks become a
  routed cluster.
- **WHEN** a service scales up/down **THEN** the cluster endpoints follow, without spurious reloads.

## Notes / risks / references
- Large surface; requires a Swarm cluster to validate. Consider scoping to replicated services first.
