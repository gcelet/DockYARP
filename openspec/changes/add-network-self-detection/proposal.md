## Why
DockYarp's reachability-aware address selection (skip a backend on a network the proxy does not share) only
kicks in when `Docker:ProxyNetworks` is **configured**. nginx-proxy/docker-gen instead **auto-detect** the
proxy's own attached networks and use those as the reachable set, so an operator gets reachability filtering
with no configuration. This is criterion (1) deferred from `e2e-multi-network`.

## What Changes
- When `Docker:ProxyNetworks` is **not** configured, DockYarp inspects its **own** container (resolved from the
  `HOSTNAME` the Docker daemon sets) once at startup and uses that container's attached network names as the
  reachable set for address selection. A configured `Docker:ProxyNetworks` still wins (no auto-detection).
- If self-inspection is not possible (no `HOSTNAME`, inspect fails), behavior is unchanged (reachability-unaware
  selection), logged.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `docker-discovery`: when `Docker:ProxyNetworks` is unset, the reachable set defaults to the proxy's own
  detected networks.

## Impact
- **Code**: `DockYarp.Docker` — new `SelfNetworkDetector` (pure: resolve own id from `HOSTNAME`; choose
  configured-vs-detected); `DockerContainerSource` inspects itself on the first listing when unconfigured and
  sets the effective proxy networks; a `DiscoveryLog` entry reports the outcome.
- **Tests (unit)**: `SelfNetworkDetector` — own-id resolution (trim/empty→null); the reachable set is the
  configured one when non-empty, else the detected one, else empty.
- **Docs**: `Docker:ProxyNetworks` is documented as auto-detected when unset.
- **Runtime / e2e**: the live self-inspection is a Docker concern (confirmable in the existing e2e logs); the
  *skip-unreachable* behavior it enables cannot be exercised under Aspire/DCP's single network — that live
  validation stays parked with `e2e-multi-network` (a non-DCP harness).
- **Owning agent**: AG-DD. Resolves `add-network-self-detection` (unblocks `e2e-multi-network` criterion 1).
