## Why

`YarpConfigMapper.BuildCluster` builds YARP destinations with `Endpoints.ToDictionary(e => e.Id, …)`, which
throws `ArgumentException` on duplicate endpoint ids and fails the whole configuration publish. Duplicate ids
are reachable from a single mislabeled input: `VIRTUAL_HOST="app.local,app.local"` adds the container twice
to one cluster, and a static-config cluster can list the same address twice — so one bad entry can crash
(re)configuration instead of being tolerated.

## What Changes

- De-duplicate a cluster's destinations by endpoint id when mapping to YARP (last definition wins), so
  duplicate endpoints never fail configuration.
- De-duplicate hosts in a comma-separated `VIRTUAL_HOST` (case-insensitive) at the source, avoiding the
  duplicate work in the first place.

## Capabilities

### Modified Capabilities
- `yarp-dynamic-config`: cluster destinations are de-duplicated by endpoint id.
- `docker-discovery`: a repeated host in `VIRTUAL_HOST` is ignored.

## Impact

- **Code**: `src/DockYarp.App/ReverseProxy` (`YarpConfigMapper`), `src/DockYarp.Docker`
  (`LabelParser.SplitHosts`).
- **Owning agent**: AG-RP / AG-DD.
