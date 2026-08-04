## Why
nginx-proxy exposes a per-vhost upstream **keepalive** setting (idle connections kept to the backend) via the
`com.github.nginx-proxy.nginx-proxy.keepalive` label. DockYarp uses YARP's default backend connection pooling
with no per-cluster override, so an operator cannot bound or tune a cluster's outgoing connections.

## What Changes
- Recognize a per-container `DOCKYARP_MAX_CONNECTIONS` (env var or label; environment wins via the existing
  merge) — the maximum concurrent connections DockYarp opens to that cluster's backend.
- Map it to YARP's per-cluster `HttpClientConfig.MaxConnectionsPerServer`. Unset (or invalid) leaves YARP's
  default pooling unchanged.
- nginx's `keepalive` (a count of *idle* keep-alive connections, default `auto`) has no 1:1 YARP knob: YARP's
  `SocketsHttpHandler` already keeps and reuses backend connections and pools them dynamically. DockYarp
  therefore exposes the connection **cap** (`MaxConnectionsPerServer`), the YARP-native pool-tuning analog,
  rather than porting the idle-count directly (which would otherwise throttle concurrency).

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `yarp-dynamic-config`: a cluster's maximum backend connections (`DOCKYARP_MAX_CONNECTIONS`) maps to YARP's
  `HttpClientConfig.MaxConnectionsPerServer`.

## Impact
- **Code**: `DockYarp.Docker` — `DockerLabels.MaxConnections`, `ContainerLabelConfig.MaxConnectionsPerServer`,
  `LabelParser` parse (`ParsePositiveInt`) + `HasInvalidMaxConnections` diagnostic, `ContainerMapper` carries
  it into `Cluster` (classic + multiports) and warns on an invalid value. `DockYarp.Core` —
  `Cluster.MaxConnectionsPerServer`. `DockYarp.App` — `YarpConfigMapper` builds a cluster `HttpClientConfig`.
- **Tests (unit)**: `LabelParser` parse + invalid diagnostic; `ContainerMapper` carries it into the cluster;
  `YarpConfigMapper` maps it to `HttpClientConfig.MaxConnectionsPerServer` (and omits `HttpClient` when unset).
- **Docs**: the site configuration reference + `docs/labels-reference.md` gain `DOCKYARP_MAX_CONNECTIONS`.
- **Runtime / e2e**: none (the mapping is config-only and fully unit-testable; the pooling behavior is YARP's).
- **Owning agent**: AG-RP. Resolves `add-backend-keepalive`.
