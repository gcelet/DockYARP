# Design — add-virtual-dest-rewrite

## Rewrite semantics (parity with nginx-proxy `VIRTUAL_DEST`)
Given a matched `VIRTUAL_PATH` and a `VIRTUAL_DEST`:
- **dest absent** → no transform; the backend receives the full path (`/api/orders`).
- **dest `/`** (or empty after trimming slashes) → strip `VIRTUAL_PATH` only (`/api/orders` → `/orders`) — the
  behavior shipped today.
- **dest `/v2`** → strip `VIRTUAL_PATH` then prepend `/v2` (`/api/orders` → `/v2/orders`).

A single shared resolver keeps the classic and multiports paths in sync:
```
PathRewrite.Resolve(dest, path):
  if dest is empty or path is empty -> (Remove: null, Add: null)
  trimmed = dest.Trim('/')
  -> (Remove: path, Add: trimmed.Length > 0 ? "/" + trimmed : null)
```
`Trim('/')` + a re-prepended leading `/` normalizes `"/v2"`, `"/v2/"`, and `"v2"` alike, and maps `"/"` to no
prepend.

## YARP mapping
`RouteTransforms` gains `PathAddPrefix`. `YarpConfigMapper.BuildTransforms` emits, **in order**:
1. `{ "PathRemovePrefix": <remove> }` — strips the matched prefix on segment boundaries.
2. `{ "PathPrefix": <add> }` — prepends the destination.

YARP applies transforms in the order listed, so remove-then-prepend produces the rewrite. Either half may be
absent (strip-only, or — reserved — prepend-only).

## Matching is unaffected
The rewrite is a forward-only transform; route selection still keys on `PathPrefix` (`VIRTUAL_PATH`). Neither
`RouteMatcher` nor the security `RouteLookup` needs changes.

## Regex `VIRTUAL_PATH` is out of scope
YARP path matching uses ASP.NET route templates, not regex, so a `~`-prefixed `VIRTUAL_PATH` cannot map to a
YARP `Path` and would need a custom matching layer (shared with `add-regex-hosts`). Today such a value is placed
into the path template literally and simply never matches — this change does not regress that. It is tracked in
the new `add-regex-virtual-path` backlog item, including nginx-proxy's rule that a regex path is incompatible
with `VIRTUAL_DEST`.
