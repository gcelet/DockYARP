## 1. Pure detector (AG-DD)
- [x] 1.1 `SelfNetworkDetector.ResolveOwnContainerId(hostname)` → trimmed hostname or null
- [x] 1.2 `SelfNetworkDetector.ChooseReachableNetworks(configured, detected)` → configured if non-empty else detected

## 2. Wire it into the source (AG-DD)
- [x] 2.1 `DockerContainerSource`: on the first listing, when configured `ProxyNetworks` is empty, resolve own id
      from `HOSTNAME`, inspect self, take `NetworkSettings.Networks` keys, set effective `proxyNetworks` (once)
- [x] 2.2 Add an `ILogger` + a `DiscoveryLog` entry reporting the detected networks (or "undetermined")

## 3. Tests (AG-DD)
- [x] 3.1 `SelfNetworkDetector`: id resolution (value / whitespace / empty→null); reachable set = configured when
      non-empty, else detected, else empty

## 4. Docs (AG-DOC)
- [x] 4.1 Site configuration reference: `Docker:ProxyNetworks` is auto-detected from the proxy's own networks when unset

## 5. Verify (AG-DD)
- [x] 5.1 Nuke `Test` gate green (unit/integration, no Docker)
