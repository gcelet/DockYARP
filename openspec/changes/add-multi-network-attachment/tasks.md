## 1. Option (AG-DD)
- [x] 1.1 `DockerDiscoveryOptions`: add `ProxyNetworks` (`IList<string>`, default empty)

## 2. Reachability-aware selection (AG-DD)
- [x] 2.1 `NetworkAddressSelector.Select`: add a required `proxyNetworks` parameter; when non-empty, restrict
      the fallback to shared (reachable) networks before the deterministic ordinal pick; preferred still wins
- [x] 2.2 `DockerContainerSource`: pass `ProxyNetworks`; when selection yields none, return an empty address if
      reachability is known (`ProxyNetworks` non-empty), else keep the container-name fallback
- [x] 2.3 `ContainerMapper`: skip a container with an empty address, warning `no reachable network address`

## 3. Tests (AG-DD)
- [x] 3.1 `NetworkAddressSelector`: shared network selected among several; only-unreachable → null; preferred
      still wins; empty `proxyNetworks` unchanged (existing cases pass an empty set)
- [x] 3.2 `ContainerMapper`: an empty-address container is skipped with the warning (no broken endpoint)

## 4. Verify (AG-DD)
- [x] 4.1 Nuke `Test` gate green
