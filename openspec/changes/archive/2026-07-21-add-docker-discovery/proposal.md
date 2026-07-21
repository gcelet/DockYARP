## Why

DockYarp must configure itself automatically from Docker, the way nginx-proxy does: containers
declare intent through labels, and the proxy reacts to their lifecycle. Without discovery, the routing
model built in `add-proxy-routing-model` stays empty. This change is the dynamic source that fills it.

## What Changes

- Add a **Docker events listener** in `DockYarp.Docker` (start/stop/die/update) with reconnect on
  daemon restart, using `Docker.DotNet`.
- Add **startup reconciliation**: enumerate already-running containers at startup and build the initial
  configuration, rather than relying on events alone (a container started before DockYarp must still
  be routed).
- Add a **label schema and parser** for nginx-proxy-compatible labels (`VIRTUAL_HOST`, `VIRTUAL_PORT`,
  `VIRTUAL_PATH`, `LETSENCRYPT_HOST`, `LETSENCRYPT_EMAIL`) plus `DOCKYARP_*` labels, producing a
  strongly-typed configuration object.
- Add **validation with safe fallback**: invalid or conflicting labels are logged and ignored without
  crashing or dropping other containers.
- Add **mapping to the routing model**: translate parsed labels + container network info into
  `proxy-routing` routes/clusters/endpoints/TLS metadata and publish them as the dynamic configuration
  source.

## Capabilities

### New Capabilities
- `docker-discovery`: Docker event subscription with reconnect, startup reconciliation, label schema +
  parsing + validation, and mapping of containers/labels into the `proxy-routing` model.

### Modified Capabilities
<!-- None. Consumes proxy-routing via its published store/merge API without changing its requirements. -->

## Impact

- **Code**: new types in `src/DockYarp.Docker` (`Services/`, models for events/labels); unit tests in
  `tests/DockYarp.Docker.Tests`.
- **Dependencies**: add `Docker.DotNet` via Central Package Management (`Directory.Packages.props`).
- **Upstream dependency**: requires `add-proxy-routing-model` (writes into its store as a dynamic source).
- **Owning agent**: AG-DD.
