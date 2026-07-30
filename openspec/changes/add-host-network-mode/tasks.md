## 1. Option + model (AG-DD)
- [x] 1.1 `DockerDiscoveryOptions`: add `HostAddress` (`string?`, default null)
- [x] 1.2 `ContainerInfo`: add `IsHostNetwork` (bool, default false)

## 2. Address resolution (AG-DD)
- [x] 2.1 New pure `BackendAddressResolver`: `IsHostNetwork(networks)` (reserved `host` key) and
      `Resolve(networks, hostAddress, selectedIp, nameFallback, proxyNetworks)`
- [x] 2.2 `DockerContainerSource`: detect host mode, set `IsHostNetwork`, run the selector only for non-host
      containers, and resolve the address via `BackendAddressResolver`
- [x] 2.3 `ContainerMapper`: host-mode empty-address skip warns that `Docker:HostAddress` is required

## 3. Split runtime validation (AG-DD)
- [x] 3.1 New backlog item `e2e-host-network-mode` (live reachability of `Docker:HostAddress` across platforms)

## 4. Tests (AG-DD)
- [x] 4.1 `BackendAddressResolver`: host detection; host-mode → host address or empty; non-host → IP / empty
      (reachability known) / name (unknown)
- [x] 4.2 `ContainerMapper`: a host-mode container with `HostAddress` routes to `host:port`; without it, it is
      skipped with the `Docker:HostAddress` warning

## 5. Verify (AG-DD)
- [x] 5.1 Nuke `Test` gate green
