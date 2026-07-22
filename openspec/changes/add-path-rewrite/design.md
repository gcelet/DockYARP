## Context

`RouteRule.Transforms.PathRemovePrefix` exists in the model but is never applied by the YARP mapper, and
discovery never derives it — so a path-based route (`VIRTUAL_PATH=/api`) forwards `/api/orders` to the
backend unchanged. nginx-proxy's `VIRTUAL_DEST` rewrites the path (its common use, `VIRTUAL_DEST=/`, strips
the matched `VIRTUAL_PATH` prefix). This wires the placeholder end to end.

## Goals / Non-Goals

**Goals:** apply `PathRemovePrefix` on the forwarded request via YARP's built-in transform; derive it from
`VIRTUAL_DEST` so `VIRTUAL_PATH=/api` + `VIRTUAL_DEST=/` strips `/api` before forwarding. No `VIRTUAL_DEST`
⇒ no rewrite.

**Non-Goals:** arbitrary path rewrites to a non-root destination (`VIRTUAL_DEST=/v2`), regex rewrites, query
rewrites — the model carries only a prefix-removal today; richer rewrites are backlog.

## Decisions

- **YARP: use the built-in `PathRemovePrefix` config transform.** `YarpConfigMapper.BuildRoute` sets
  `RouteConfig.Transforms` to a single `{ "PathRemovePrefix": prefix }` entry when the rule carries one, and
  `null` otherwise. Verified via Microsoft Learn: `RouteConfig.Transforms` is
  `IReadOnlyList<IReadOnlyDictionary<string,string>>?` and `PathRemovePrefix` is a first-class request
  transform (segment-boundary match, no-op when the prefix does not match). No custom transform code, and it
  composes with the existing programmatic forwarded-headers transforms.
- **Docker: `VIRTUAL_DEST` derives the prefix-strip.** A new `VirtualDest` label; the parser sets
  `ContainerLabelConfig.PathRemovePrefix = VIRTUAL_PATH` when both `VIRTUAL_DEST` and `VIRTUAL_PATH` are
  present (i.e. the `VIRTUAL_DEST=/` prefix-strip). `HostGroup.BuildRoute` builds `RouteTransforms` from it.
- **Model unchanged.** `RouteTransforms.PathRemovePrefix` already models exactly this.

## Risks / Trade-offs

- A non-root `VIRTUAL_DEST` is treated as a prefix-strip (closest supported behavior) rather than a full
  rewrite; documented as a current limitation so it is not mistaken for arbitrary rewriting.

## Migration Plan

Additive: one new label, one optional config field, mapper wiring on both sides. No config/persisted state.

## Open Questions

- Arbitrary `VIRTUAL_DEST` rewrites (non-root) and query/regex rewrites — deferred to a later routing change.
