## 1. Regex in the host matcher (AG-RP)
- [x] 1.1 `HostPatternKind`: add `Regex` (lowest precedence)
- [x] 1.2 `HostPattern`: classify `~`-prefixed patterns as regex; compile once
      (`Compiled | CultureInvariant`, `matchTimeout`), fail closed on timeout, invalid regex never matches
- [x] 1.3 `HostPattern.Parse`: memoize in a static `ConcurrentDictionary` (avoids per-request recompile)
- [x] 1.4 `RouteMatcher`: add a regex tier after the trailing tier

## 2. Proxy wiring (AG-RP)
- [x] 2.1 `YarpConfigMapper`: unify the non-native host metadata key to `DockYarp.HostPattern` (trailing + regex);
      give regex routes a lower `Order` than trailing
- [x] 2.2 `DockYarpHostMatcherPolicy`: read `DockYarp.HostPattern` and classify via `HostPattern.Parse`

## 3. Tests (AG-RP)
- [x] 3.1 `HostPattern`: regex match / non-match; invalid regex never matches
- [x] 3.2 `RouteMatcher`: regex route matches; exact/leading/trailing win over regex
- [x] 3.3 `DockYarpHostMatcherPolicy`: regex-metadata candidate invalidated for a non-matching host, kept for a
      matching one

## 4. Verify (AG-RP)
- [x] 4.1 Nuke `Test` gate green
