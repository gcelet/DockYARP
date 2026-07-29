## Why
nginx-proxy reaches backends spread across several Docker networks and tolerates containers on networks it
cannot reach. DockYarp selects one network deterministically but is not reachability-aware: when the proxy does
not share a container's chosen network it can forward to an unreachable IP, and a backend reachable on no
shared network produces a broken endpoint instead of being skipped.

## What Changes
- Add a `Docker:ProxyNetworks` option: the set of Docker networks the proxy is attached to. Empty preserves
  today's behavior.
- Make address selection reachability-aware: when `ProxyNetworks` is known, the fallback (no preferred network)
  chooses the first deterministic **shared** network's IP, and yields none when the backend shares no reachable
  network — rather than pointing at an unreachable address.
- A container with no reachable network address is skipped with a clear warning instead of producing a broken
  route/cluster endpoint.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `docker-discovery`: address selection is reachability-aware across multiple networks, and unreachable
  backends are skipped rather than routed.

## Impact
- **Code**: `DockYarp.Docker` — `DockerDiscoveryOptions.ProxyNetworks`, `NetworkAddressSelector.Select`
  (reachability filter), `DockerContainerSource` (pass proxy networks; empty address when known-unreachable),
  `ContainerMapper` (skip + warn on an empty address).
- **Tests**: `NetworkAddressSelector` (shared network selected; only-unreachable → none; preferred still wins;
  empty `ProxyNetworks` unchanged), `ContainerMapper` (empty-address container skipped with warning).
- **Deferred**: auto-detecting the proxy's own network memberships at runtime (instead of configuring
  `ProxyNetworks`) and validating live skip behavior needs a Docker-capable session — noted for a runtime/e2e
  follow-up, consistent with the testing pyramid.
- **Owning agent**: AG-DD. Resolves `add-multi-network-attachment`.
