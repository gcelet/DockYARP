## 1. Shared regex cache (AG-RP)
- [x] 1.1 `DockYarp.Core`: add `CompiledRegexCache.IsMatch(body, input)` (compile once, cache, bounded timeout,
      fail closed on timeout/invalid)
- [x] 1.2 `HostPattern`: refactor its regex kind to use `CompiledRegexCache`

## 2. Path matching (AG-RP)
- [x] 2.1 `RouteMatcher.PathMatches`: a `~`-prefixed `PathPrefix` matches by regex; otherwise ordinal prefix
- [x] 2.2 `YarpConfigMapper`: a regex `PathPrefix` emits a catch-all `Path` + `RouteConfig.Metadata["DockYarp.PathRegex"]`
- [x] 2.3 `DockYarpPathMatcherPolicy` (`MatcherPolicy` + `IEndpointSelectorPolicy`): invalidate candidates whose
      request path does not match the regex; register in DI
- [x] 2.4 `PathRewrite.Resolve`: return no rewrite when the path is a regex (`VIRTUAL_DEST` incompatible)

## 3. Tests (AG-RP)
- [x] 3.1 `RouteMatcher`: a regex path matches / rejects; a prefix path wins over a regex path
- [x] 3.2 `DockYarpPathMatcherPolicy`: path-regex candidate invalidated for a non-matching path, kept for a match
- [x] 3.3 `PathRewrite`/`LabelParser`: a regex `VIRTUAL_PATH` with `VIRTUAL_DEST` yields no rewrite

## 4. Verify (AG-RP)
- [x] 4.1 Nuke `Test` gate green
