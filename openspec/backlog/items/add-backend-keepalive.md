---
id: add-backend-keepalive
capability: yarp-dynamic-config
agent: AG-RP
tier: B-runtime
priority: low
status: backlog
nginx-proxy: com.github.nginx-proxy.nginx-proxy.keepalive label
provenance: 2026-07-31 parity re-analysis
---

## Why
nginx-proxy exposes a per-vhost upstream **keepalive** setting (idle connections kept to the backend), tunable
via the `com.github.nginx-proxy.nginx-proxy.keepalive` label. DockYarp uses YARP's default connection pooling
with no per-cluster override.

## nginx-proxy behavior
- Label `keepalive` sets the nginx upstream `keepalive` directive (max idle keep-alive connections) or
  `disabled`; default `auto` (≈ 2× server count).

## DockYarp today
- YARP's `SocketsHttpHandler` manages backend connection pooling with framework defaults; there is no
  per-cluster keepalive / pool tuning exposed.

## Proposed change (sketch)
- Add a per-cluster option (label + config) mapping to YARP's `HttpClientConfig` (e.g.
  `MaxConnectionsPerServer`, `PooledConnectionLifetime`/`PooledConnectionIdleTimeout`) — the YARP-native analog
  of upstream keepalive. Default = YARP defaults (unchanged) when unset.

## Acceptance criteria (→ scenarios)
- **WHEN** a keepalive/pool option is set for a cluster **THEN** the mapped YARP `HttpClientConfig` reflects it.
- **WHEN** it is unset **THEN** the cluster uses YARP's default pooling (unchanged).

## Notes / risks / references
- Mapping is config-only and unit-testable; the actual pooling behavior is YARP's. Decide which knobs to expose
  (idle timeout vs max connections vs lifetime).
