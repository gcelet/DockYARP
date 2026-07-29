## Why
nginx-proxy treats a `VIRTUAL_HOST` beginning with `~` as a **regex** `server_name` (`~^app-\d+\.example\.com$`),
commonly used for nip.io/sslip.io-style dynamic hosts. DockYarp has no regex host matching, so such a value is
parsed as an exact host and never matches. This reuses the `add-trailing-wildcard-hosts` foundation (the
`DockYarpHostMatcherPolicy` + `HostPattern`) and adds the regex form.

## What Changes
- **Core `HostPattern`**: a `~`-prefixed pattern classifies as `Regex`. The body is compiled once
  (`RegexOptions.Compiled | CultureInvariant`) and **cached**, with a bounded `matchTimeout` (ReDoS guard);
  `Matches` fails closed on a timeout, and an invalid regex compiles to a never-matching pattern (never crashes
  discovery). Precedence becomes exact > leading wildcard > trailing wildcard > regex.
- **`RouteMatcher`**: a regex tier after the trailing tier.
- **Proxy**: the non-native host metadata key is unified to `DockYarp.HostPattern` (carrying the raw pattern),
  set for trailing **and** regex; `DockYarpHostMatcherPolicy` classifies it via `HostPattern.Parse`. Regex host
  routes get a lower `Order` than trailing so wildcard beats regex, per nginx precedence.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `proxy-routing`: a `VIRTUAL_HOST` may be a `~`-prefixed regex, matched via the endpoint-selector policy, ranked
  below exact and wildcard hosts, with compiled/cached regex and a ReDoS-bounded match timeout.

## Impact
- **Code**: `DockYarp.Core` (`HostPattern` regex + cache, `HostPatternKind.Regex`, `RouteMatcher` regex tier),
  `DockYarp.App` (`YarpConfigMapper` metadata key unified + regex ordering, `DockYarpHostMatcherPolicy` key).
- **Tests**: `HostPattern` (regex match / non-match / invalid → never matches), `RouteMatcher` (regex tier +
  precedence below wildcard), `DockYarpHostMatcherPolicy` (regex host invalidation).
- **Reuses**: the foundation from `add-trailing-wildcard-hosts`. Shares the ReDoS-safe regex approach with the
  sibling `add-regex-virtual-path`.
- **Owning agent**: AG-RP. Resolves `add-regex-hosts`.
