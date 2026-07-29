## Why
nginx-proxy allows a `VIRTUAL_PATH` to be a **regex location** (`~^/(app1|alt1)/`). DockYarp maps `VIRTUAL_PATH`
to a YARP route-template `Path`, which is not a regex, so a `~`-prefixed path is placed into the template
literally and never matches. This completes the custom-matching family, reusing the host-matching foundation
(the `MatcherPolicy` + route-metadata channel + the ReDoS-safe compiled-regex cache).

## What Changes
- **Core**: extract the compiled-regex machinery (compile once, cache, bounded match timeout, fail closed on
  timeout/invalid) from `HostPattern` into a shared `CompiledRegexCache`; `HostPattern` reuses it. `RouteMatcher`
  path matching treats a `~`-prefixed `PathPrefix` as a regex.
- **Proxy (YARP extension point)**: a regex path route is emitted with a catch-all `Path` and carries the regex
  in `RouteConfig.Metadata` (`DockYarp.PathRegex`); a `DockYarpPathMatcherPolicy` invalidates candidates whose
  request path does not match. Prefix paths keep using the native route template and remain more specific.
- **nginx incompatibility**: `VIRTUAL_DEST` is ignored for a regex `VIRTUAL_PATH` (a regex location has no fixed
  prefix to strip/rewrite), matching nginx-proxy's rule.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `proxy-routing`: a `VIRTUAL_PATH` may be a `~`-prefixed regex, matched via the endpoint-selector policy with a
  ReDoS-bounded compiled regex; prefix paths remain more specific, and `VIRTUAL_DEST` does not apply to a regex
  path.

## Impact
- **Code**: `DockYarp.Core` (`CompiledRegexCache`, `HostPattern` refactor, `RouteMatcher.PathMatches`),
  `DockYarp.Docker` (`PathRewrite` skips a regex path), `DockYarp.App` (`YarpConfigMapper` path metadata +
  `DockYarpPathMatcherPolicy` + DI).
- **Tests**: `CompiledRegexCache`/`RouteMatcher` (regex path match / non-match, prefix still wins),
  `DockYarpPathMatcherPolicy` (path invalidation), `PathRewrite` (dest ignored for a regex path).
- **Completes**: the custom host/path matching family (`add-trailing-wildcard-hosts`, `add-regex-hosts`, this).
- **Owning agent**: AG-RP. Resolves `add-regex-virtual-path`.
