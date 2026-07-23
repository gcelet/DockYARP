## Context

`ContainerMapper` maps a container to a single port/path per host (clusters keyed by host). nginx-proxy's
`VIRTUAL_HOST_MULTIPORTS` needs several `host+path → port` mappings from one container. The classic path is
heavily used and evolved, so multiports is added as an isolated producer rather than reshaping the existing
host grouping.

## Goals / Non-Goals

**Goals:** parse the nginx-proxy YAML and produce one route + cluster per `(host, path)` entry, targeting the
container on the entry's port/scheme; apply container-level attributes; leave the classic mapping untouched.

**Non-Goals (deferred):** arbitrary `dest` path rewrites (only prefix-strip, as with `VIRTUAL_DEST`),
non-http/https protocols, and hot-reload of the label beyond the normal reconcile.

## Decisions

- **`YamlDotNet`** parses the label. A pure `MultiportParser.TryParse(yaml, out entries, out error)` yields
  `MultiportEntry(host, path, port, scheme, dest)`; malformed YAML returns `false` (the mapper warns).
- **Isolated producer**: `ContainerMapper.Map` branches — a container with the label goes through a new
  multiports producer (groups keyed by `clusterId(host, path)` = `host` for root path, else `host+path`);
  every other container uses the existing classic host grouping **unchanged**. Health checks and skip
  warnings are shared. Replicas aggregate per `(host, path)`.
- **Container-level attributes**: `LabelParser.ParseCommon(labels)` reads the non-routing attributes (auth,
  LB, priority, timeout, body size, client cert, LETSENCRYPT, HTTPS method, HSTS) into a `ContainerLabelConfig`
  with no host/port. Each multiports route/cluster applies them; TLS metadata is attached when the entry's
  host is listed in `LETSENCRYPT_HOST`. `dest` maps to a `PathRemovePrefix` (strip) like `VIRTUAL_DEST`.

## Risks / Trade-offs

- A multiports entry and a classic container could both produce the same cluster id; the merger already
  reports and resolves such conflicts. Documented.
- YAML parsing adds a dependency; it is confined to the Docker module and guarded (failures degrade to no
  multiports routes for that container).

## Migration Plan

Additive: new label, YAML dependency, a parser, and a second producer in the mapper. The classic mapping and
all existing behavior are unchanged when the label is absent.

## Open Questions

- Rich `dest` rewrites and additional protocols (grpc/fastcgi) — deferred with the earlier proto/dest work.
