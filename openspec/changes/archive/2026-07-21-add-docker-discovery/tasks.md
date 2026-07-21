## 1. Docker client & package (AG-DD)

- [x] 1.1 Add `Docker.DotNet` `PackageVersion` in `Directory.Packages.props` and reference it in `DockYarp.Docker`
- [x] 1.2 Add a Docker client factory (socket/named-pipe endpoint, from configuration) with an abstraction for testing

## 2. Event subscription & resilience (AG-DD)

- [x] 2.1 Implement the event subscription (start/stop/die/update) as a hosted background service
- [x] 2.2 Normalize Docker events into an internal lifecycle event model
- [x] 2.3 Implement reconnect with capped exponential backoff and connection-state logging
- [x] 2.4 On (re)connect, trigger a full reconciliation so missed changes converge

## 3. Startup reconciliation (AG-DD)

- [x] 3.1 Enumerate running containers at startup and build the initial dynamic contribution
- [x] 3.2 Publish the initial contribution to the proxy-routing store before serving traffic

## 4. Label parsing & validation (AG-DD)

- [x] 4.1 Define the label schema (supported labels, types, defaults, validation rules) as typed config
- [x] 4.2 Implement a pure parser: label dictionary → strongly-typed config (nginx-proxy + `DOCKYARP_*`)
- [x] 4.3 Implement default target-port inference (single exposed port) and require explicit `VIRTUAL_PORT` otherwise
- [x] 4.4 Implement validation with structured logging and safe skip of invalid/conflicting containers

## 5. Mapping to routing model (AG-DD)

- [x] 5.1 Map parsed config + container network info into proxy-routing routes/clusters/endpoints
- [x] 5.2 Aggregate replicas of the same VIRTUAL_HOST into one cluster, keyed by container id
- [x] 5.3 Populate per-host TLS metadata from `LETSENCRYPT_HOST`/`LETSENCRYPT_EMAIL`
- [x] 5.4 Publish as the dynamic configuration source via the proxy-routing merge API

## 6. Tests (AG-DD)

- [x] 6.1 Unit tests for the parser (valid, invalid, missing, conflicting labels, port inference)
- [x] 6.2 Unit tests for mapping (replica aggregation, endpoint add/remove, TLS metadata)
- [x] 6.3 Unit tests for lifecycle handling with a mocked Docker client (start/stop/die/update)
- [x] 6.4 Unit tests for reconnect + reconcile behavior

## 7. Documentation (AG-DD)

- [x] 7.1 Write `docs/labels-reference.md` (full label reference with examples) replacing the stub
- [x] 7.2 Document event handling and failure modes (daemon restart, network issues)
