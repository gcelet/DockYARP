## Why

nginx-proxy's `VIRTUAL_HOST_MULTIPORTS` lets one container serve several `host + path` combinations, each to a
different container port (and proto/dest). DockYarp maps a container to a single port/path per host, so it
cannot expose, say, `app.local/` → :8080 and `app.local/api` → :9000 from one container.

## What Changes

- Parse `VIRTUAL_HOST_MULTIPORTS` (the nginx-proxy YAML: `host: { path: { port, proto, dest } }`) into
  per-entry `(host, path, port, proto, dest)` mappings.
- Map each entry to a route (host + path) and a cluster keyed by `host`/`host+path`, targeting the container
  on the entry's port and scheme; replicas of the same entry aggregate. Container-level attributes (auth,
  load balancing, priority, timeout, body size, client cert, and TLS when the host is a `LETSENCRYPT_HOST`)
  apply to the generated routes. The classic `VIRTUAL_HOST`/`VIRTUAL_PORT` mapping is unchanged for
  containers without the multiports label.

## Capabilities

### Modified Capabilities
- `docker-discovery`: a container may declare multiple host/path→port mappings via `VIRTUAL_HOST_MULTIPORTS`.

## Impact

- **Code**: `src/DockYarp.Docker` (`VIRTUAL_HOST_MULTIPORTS` label, YAML parser, per-entry mapping in
  `ContainerMapper`; a `LabelParser.ParseCommon` for container-level attributes).
- **Dependencies**: `YamlDotNet` via CPM.
- **Deferred**: arbitrary `dest` rewrites (only prefix-strip, as with `VIRTUAL_DEST`) and non-http/https
  protocols.
- **Owning agent**: AG-DD.
