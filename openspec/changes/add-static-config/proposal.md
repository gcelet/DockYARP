## Why

DockYarp models static configuration (`ConfigSource.Static`, with `Static` winning over `Dynamic` in
`RouteConfigMerger`), but nothing ever produces a static contribution — so operators cannot declare routes to
backends that aren't Docker-discovered (external services), and the precedence machinery is never exercised.

## What Changes

- Read a **static configuration file** (JSON at `StaticConfig:Path`) via the filesystem abstraction into a
  `ConfigContribution(Static)` of routes and clusters.
- **Merge** it with Docker discovery (static wins on conflicts) when discovery is enabled; **apply** it at
  startup when discovery is disabled — so static config works with or without Docker.

## Capabilities

### Modified Capabilities
- `proxy-routing`: a static configuration file is a first-class configuration source, merged with
  precedence over dynamic discovery.

## Impact

- **Code**: `src/DockYarp.Core` (`IStaticConfigProvider`, an empty default), `src/DockYarp.App`
  (JSON provider over `IFileSystem`, a startup applier, wiring), `src/DockYarp.Docker`
  (`DiscoveryReconciler` merges the static contribution).
- **Deferred**: hot-reload on file change, YAML, and advanced per-route fields (TLS/auth/transforms) in the
  static file — routes carry host/path/cluster/priority and clusters carry addresses/LB for now.
- **Owning agent**: AG-RP.
