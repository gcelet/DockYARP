## Why

nginx-proxy lets one container serve several hostnames via a comma-separated `VIRTUAL_HOST`. DockYarp
treats the whole value as a single host pattern, so multi-host containers are not supported.

## What Changes

- Split `VIRTUAL_HOST` on commas (trimming whitespace) into multiple hosts; the container is mapped to one
  route per host (sharing its port/path/TLS/auth), each aggregating into its per-host cluster.
- Empty entries are ignored; a container with no valid host is skipped and logged (unchanged behavior).

## Capabilities

### Modified Capabilities
- `docker-discovery`: a container may declare multiple comma-separated hosts.

## Impact

- **Code**: `src/DockYarp.Docker` (`LabelParser` host parsing → list; `ContainerMapper` fan-out per host).
- **Owning agent**: AG-DD.
