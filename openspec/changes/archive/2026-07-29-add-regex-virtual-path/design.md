# Design — add-regex-virtual-path

## Shared compiled-regex cache
`HostPattern` currently owns the regex compile/cache/ReDoS logic. That machinery is extracted into
`CompiledRegexCache` (Core): `IsMatch(body, input)` compiles the body once
(`RegexOptions.Compiled | CultureInvariant`, bounded `matchTimeout`), caches it keyed by body, and fails closed
on a timeout or an invalid expression. `HostPattern`'s regex kind is refactored to call it, so there is one
ReDoS policy for both host and path regexes (DRY; the existing host tests guard the refactor).

## Path matching
A regex `VIRTUAL_PATH` flows through as `RouteRule.PathPrefix` with its `~` prefix intact (the label parser
already copies `VIRTUAL_PATH` verbatim). Matching branches on the leading `~`:
- **`RouteMatcher.PathMatches`**: `~` → `CompiledRegexCache.IsMatch(pattern[1..], path)`; otherwise the existing
  ordinal prefix check.
- **YARP**: a route-template `Path` cannot express a regex, so `YarpConfigMapper` emits a catch-all `Path` and
  records the regex body in `RouteConfig.Metadata` (`DockYarp.PathRegex`); a `DockYarpPathMatcherPolicy`
  (mirroring the host policy) invalidates candidates whose request path does not match. Prefix-path routes keep
  their specific template and so remain more specific than a catch-all regex route.

Two independent selector policies (host, path) compose: a candidate survives only if both its host and path
metadata (when present) match the request.

## VIRTUAL_DEST incompatibility
A regex location has no fixed prefix to strip, so `VIRTUAL_DEST` is meaningless with it (nginx-proxy forbids the
combination). `PathRewrite.Resolve` returns no rewrite when the path begins with `~`, so a stray `VIRTUAL_DEST`
is ignored rather than producing a broken transform.

## Precedence and ReDoS
- Prefix paths (specific template) win over regex paths (catch-all + metadata); within regex paths, route
  `Order`/priority applies as elsewhere.
- ReDoS is bounded by the shared match timeout; a pathological or invalid expression never routes and never
  stalls the request path.

## Testing
- `CompiledRegexCache` via `RouteMatcher`: a regex path matches the intended paths and rejects others; a prefix
  path still wins over a regex path.
- `DockYarpPathMatcherPolicy`: a path-regex candidate is invalidated for a non-matching path and kept for a
  matching one.
- `PathRewrite`: `VIRTUAL_DEST` with a regex `VIRTUAL_PATH` yields no rewrite.
