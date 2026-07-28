## Why
nginx-proxy's `VIRTUAL_DEST` rewrites the matched `VIRTUAL_PATH` prefix to an **arbitrary** destination path
(`/api` → `/v2`). DockYarp treats `VIRTUAL_DEST` as a boolean strip flag: any non-empty value strips
`VIRTUAL_PATH` and the destination value is **ignored**, so `VIRTUAL_DEST=/v2` yields `/orders` instead of
`/v2/orders`. Mounts that need a non-trivial rewrite are unsupported.

`VIRTUAL_PATH` may also be an nginx **regex** location (`~^/(app1|alt1)/`). YARP path matching uses route
templates, not regex, so regex locations need a custom matching layer — the same family of work as
`add-regex-hosts`. That is split into its own backlog item rather than bundled here.

## What Changes
- **Model**: add `RouteTransforms.PathAddPrefix` (the destination prefix to prepend after stripping).
- **Parsing**: resolve `VIRTUAL_DEST` into a (remove, add) pair — strip `VIRTUAL_PATH` and, for a non-root dest,
  prepend the normalized destination. A `/` (or empty-after-trim) dest keeps today's pure strip.
- **Mapping**: both the classic and multiports route builders carry the add-prefix; the YARP mapper emits a
  `PathRemovePrefix` transform followed by a `PathPrefix` transform (applied in order → rewrite).
- **Split**: new backlog item `add-regex-virtual-path` for regex `VIRTUAL_PATH` locations (and nginx's
  regex-path ↔ `VIRTUAL_DEST` incompatibility).

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `yarp-dynamic-config`: the path rewrite transform can rewrite a matched prefix to an arbitrary destination,
  not only strip it.

## Impact
- **Code**: `DockYarp.Core` (`RouteTransforms`), `DockYarp.Docker` (`ContainerLabelConfig`, `LabelParser`,
  `ContainerMapper`, a shared `PathRewrite` resolver), `DockYarp.App` (`YarpConfigMapper`).
- **Tests**: `DockYarp.Docker.Tests` (dest → remove+add), `DockYarp.Core.Tests`/App mapper (two transforms
  emitted in order).
- **Backlog**: resolves the arbitrary-rewrite half of `add-virtual-dest-rewrite`; regex `VIRTUAL_PATH` moves to
  `add-regex-virtual-path`. Parity row split at archive.
- **Owning agent**: AG-RP.
