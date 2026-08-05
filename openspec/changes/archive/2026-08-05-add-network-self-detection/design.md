# Design — add-network-self-detection

## Pure core (unit-tested) vs live inspect (runtime)
`SelfNetworkDetector` holds the testable logic, decoupled from Docker.DotNet:
- `ResolveOwnContainerId(string? hostname)` → the trimmed hostname, or `null` when empty. The Docker daemon sets
  `HOSTNAME` to the container's (short) id by default, and `InspectContainerAsync` accepts an id prefix.
- `ChooseReachableNetworks(configured, detected)` → `configured` when non-empty, else `detected`. This is the
  "configured wins; otherwise auto-detected" rule.

`DockerContainerSource` does the live part: on the **first** `ListRunningContainersAsync`, if the configured
`ProxyNetworks` is empty, it resolves its own id from `HOSTNAME`, `InspectContainerAsync`es itself, takes the
`NetworkSettings.Networks` keys as the detected set, and sets the effective `proxyNetworks` via
`ChooseReachableNetworks`. Attempted once; a missing `HOSTNAME` or a failed inspect leaves the effective set
empty (reachability-unaware, prior behavior), logged via `DiscoveryLog`.

```
first ListRunningContainersAsync:
  if configuredProxyNetworks empty and not yet attempted:
     id = ResolveOwnContainerId(HOSTNAME)
     detected = id is null ? [] : InspectContainerAsync(id).NetworkSettings.Networks.Keys
     proxyNetworks = ChooseReachableNetworks(configuredProxyNetworks, detected)
     log detected (or "undetermined")
```

## Why HOSTNAME (not /proc)
`HOSTNAME` is the Docker-standard, cross-platform signal and needs no `/proc` parsing. A custom `--hostname`
breaks it; docker-gen falls back to `/proc/1/cpuset` etc. for that case — deferred here as a robustness
follow-up (the common path sets `HOSTNAME` = container id).

## Testing boundary
The pure helpers are unit-tested. The live self-inspect is a Docker integration (not unit-tested), confirmable
in the existing e2e logs (DockYarp logs its detected networks). The *skip-unreachable* behavior this enables
needs a backend on an unreachable network, which Aspire/DCP's single managed network cannot provide — that live
check remains with `e2e-multi-network` (non-DCP harness).

## Out of scope
- `/proc`-based id resolution for a custom hostname (robustness follow-up).
- Any change to the selection algorithm itself (`NetworkAddressSelector`) — only how the reachable set is sourced.
