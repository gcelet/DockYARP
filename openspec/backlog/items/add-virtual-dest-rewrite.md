---
id: add-virtual-dest-rewrite
capability: yarp-dynamic-config
agent: AG-RP
tier: A-structural
priority: medium
status: backlog
nginx-proxy: VIRTUAL_PATH + VIRTUAL_DEST
provenance: this parity pass (matrix row was ⚠️ prefix-strip only)
---

## Why
`VIRTUAL_DEST` in nginx-proxy rewrites the matched `VIRTUAL_PATH` prefix to an arbitrary destination, and
`VIRTUAL_PATH` may be a regex location. DockYarp only strips the prefix (`VIRTUAL_DEST=/`), so mounts that
need a non-trivial rewrite (e.g. `/api → /v2`) or regex locations are unsupported.

## nginx-proxy behavior
- `VIRTUAL_PATH` mounts a container at an absolute path; supports regex locations (`~^/(app1|alt1)/`).
- `VIRTUAL_DEST` (default empty) rewrites/strips the `VIRTUAL_PATH` prefix when proxying upstream. Not
  compatible with a regex `VIRTUAL_PATH`.

## DockYarp today
Prefix-strip only: `ResolvePathRewrite` supports `VIRTUAL_DEST=/` (strip the `VIRTUAL_PATH`), see
`src/DockYarp.Docker/Labels/LabelParser.cs:111-118`; arbitrary rewrites and regex paths are ⛔. The rewrite is
applied as a YARP path transform (`src/DockYarp.App/ReverseProxy/`).

## Proposed change (sketch)
Extend the path-rewrite resolution to accept an arbitrary destination prefix and emit the matching YARP
`PathRemovePrefix` + `PathPrefix` (or `PathSet`) transforms. Add optional regex `VIRTUAL_PATH` support with a
`PathPattern`/regex transform. Keep the `/`-strip behavior as the default.

## Acceptance criteria (→ scenarios)
- **WHEN** `VIRTUAL_PATH=/api` and `VIRTUAL_DEST=/v2` and a request hits `/api/orders` **THEN** the backend
  receives `/v2/orders`.
- **WHEN** `VIRTUAL_PATH=/api` and `VIRTUAL_DEST=/` **THEN** the backend receives `/orders` (unchanged
  behavior).
- **WHEN** a regex `VIRTUAL_PATH` is set with `VIRTUAL_DEST` **THEN** the label is rejected with a warning
  (parity with nginx-proxy's incompatibility) — or documented behavior if we choose to support it.

## Notes / risks / references
- Decide the regex-path + rewrite interaction explicitly (nginx-proxy forbids the combination).
