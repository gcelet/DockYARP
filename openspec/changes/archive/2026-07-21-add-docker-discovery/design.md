## Context

Discovery is DockYarp's dynamic configuration source. It talks to the Docker daemon (unix socket or
named pipe) via `Docker.DotNet`, reacts to lifecycle events, and must also cope with the daemon being
unavailable or restarting. It writes exclusively through the `proxy-routing` store/merge API defined in
`add-proxy-routing-model`; it never builds YARP config directly. nginx-proxy label compatibility is a
hard requirement so existing compose files keep working.

## Goals / Non-Goals

**Goals:**
- Reliable event-driven updates plus a startup scan so pre-existing containers are routed.
- Correct, well-documented label parsing (nginx-proxy compat + `DOCKYARP_*`) with safe validation.
- Resilience to daemon restarts / connection drops (reconnect + reconcile).

**Non-Goals:**
- No YARP wiring (→ `add-yarp-dynamic-config`), no certificate acquisition (→ `add-tls-acme`).
- No Docker Swarm / multi-daemon support yet (single daemon endpoint).

## Decisions

- **`Docker.DotNet`** as the client (idiomatic .NET, async streaming events). Alternative: raw HTTP over
  the socket — rejected (reinventing the client). Recorded to satisfy the original DD-EVENTS design task.
- **Reconcile-on-connect as the source of truth.** Both startup and post-reconnect run the same full
  container enumeration → build a complete dynamic contribution → publish. Events then apply incremental
  deltas. Rationale: one code path guarantees convergence and covers the "missed events during outage" gap.
- **Endpoint identity = container id.** Adding/removing endpoints keys on container id so replicas of the
  same `VIRTUAL_HOST` aggregate into one cluster and stop events remove the right endpoint.
- **Parsing is pure and testable.** Label dictionary → typed config is a pure function with no Docker
  dependency, so unit tests cover valid/invalid/conflicting combinations with a mocked client.
- **Backoff on reconnect** with capped exponential delay; log connection state transitions.

## Risks / Trade-offs

- Full re-enumeration on every reconnect is slightly heavier than replaying missed events → acceptable and
  far simpler/correct; container counts are modest. Mitigation: value-equality in the store avoids churn.
- Port inference when `VIRTUAL_PORT` is absent is ambiguous for multi-port containers → require explicit
  `VIRTUAL_PORT` in that case and log when inference is impossible.

## Migration Plan

Additive. Adds `Docker.DotNet` to `Directory.Packages.props`. No data migration.

## Open Questions

- Socket/pipe path configuration and permissions inside the container image (revisit in `add-deployment`).
- Whether to debounce bursts of events (many containers starting at once) — start simple, measure later.
