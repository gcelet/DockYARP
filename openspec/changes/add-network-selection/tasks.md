## 1. Selection logic & options (AG-DD)

- [x] 1.1 Add `NetworkAddressSelector.Select(networkAddresses, preferredNetwork)`: preferred wins, else deterministic ordinal, skip `ingress`, else null
- [x] 1.2 Add `DockerDiscoveryOptions.PreferredNetwork`

## 2. Source wiring (AG-DD)

- [x] 2.1 `DockerContainerSource` stores the preferred network and routes `ResolveAddress` through the selector (build `name → IP` from `NetworkSettings.Networks`)

## 3. Tests & docs (AG-DD)

- [x] 3.1 Selector tests: preferred network wins; ingress skipped; deterministic ordinal choice; empty → null
- [x] 3.2 Document network selection in `docs/docker-discovery.md`
- [x] 3.3 Build + full test suite green via the Nuke CLI
