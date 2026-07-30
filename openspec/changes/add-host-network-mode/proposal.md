## Why
nginx-proxy supports backends running in Docker **host** network mode. Such a container has no container
network IP, so DockYarp's IP-based address selection leaves it effectively unroutable. Host-mode backends are
common for performance-sensitive or host-integrated services and should be reachable.

## What Changes
- Add a `Docker:HostAddress` option: how the proxy reaches the Docker host (e.g. `host.docker.internal` or the
  host-gateway IP).
- Detect host-network containers (a reserved `host` network entry) and target `Docker:HostAddress` on the
  backend's port instead of a container IP.
- A host-network backend requires `VIRTUAL_PORT` (no port can be inferred) — already enforced by port
  resolution (host mode exposes no ports) — and is skipped with a clear warning when `Docker:HostAddress` is not
  configured.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `docker-discovery`: host-network backends are routable via a configured Docker host address.

## Impact
- **Code**: `DockYarp.Docker` — `DockerDiscoveryOptions.HostAddress`, `ContainerInfo.IsHostNetwork`, a pure
  `BackendAddressResolver` (host-mode detection + host/IP/name address decision), `DockerContainerSource`
  (wire it), `ContainerMapper` (host-mode-specific warning on an empty address).
- **Tests**: `BackendAddressResolver` (host detection; host-mode→host address or empty; non-host→IP / empty /
  name), `ContainerMapper` (host-mode routed to the host address; host-mode without `HostAddress` skipped).
- **Deferred**: validating that `Docker:HostAddress` is actually reachable from inside the proxy container
  across platforms (Docker Desktop `host.docker.internal` vs Linux `host-gateway`) needs a Docker-capable
  session — new backlog item `e2e-host-network-mode`.
- **Owning agent**: AG-DD. Resolves `add-host-network-mode`.
