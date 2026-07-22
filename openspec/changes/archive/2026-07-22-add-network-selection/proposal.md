## Why

A container attached to several Docker networks has several IPs, and only the IP on a network the proxy
shares is reachable. DockYarp picks the **first** network IP it finds (`ResolveAddress`), which is
non-deterministic and, on a Swarm host, can pick the unroutable `ingress` IP — so traffic is forwarded to
the wrong address. nginx-proxy resolves this by using the IP on the network shared with the proxy.

## What Changes

- Add a configurable **preferred network** (`Docker:PreferredNetwork`): when a container is attached to it,
  that network's IP is used.
- Otherwise select **deterministically** among the container's networks and **skip the Swarm `ingress`
  network**; fall back to the container name (resolvable on a shared network) when no address is available.
- Extract the selection into a pure, unit-testable helper.

## Capabilities

### Modified Capabilities
- `docker-discovery`: the forwarded container address is selected by network (preferred network, ingress
  skipped, deterministic) instead of "first network wins".

## Impact

- **Code**: `src/DockYarp.Docker` (`NetworkAddressSelector`, `DockerContainerSource.ResolveAddress`,
  `DockerDiscoveryOptions.PreferredNetwork`).
- **Deferred**: `NETWORK_ACCESS=internal` (access control — belongs with security), host-network mode, and
  IPv6 preference.
- **Owning agent**: AG-DD.
