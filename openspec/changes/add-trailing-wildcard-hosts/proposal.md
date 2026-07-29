## Why
nginx-proxy matches a **trailing** wildcard `VIRTUAL_HOST` such as `foo.bar.*` (any TLD/suffix). DockYarp
supports only exact and **leading** `*.suffix` wildcards; YARP's native `Match.Hosts` likewise supports only a
leading `*.`. A trailing wildcard is currently parsed as an exact host and never matches. This change adds
trailing-wildcard support **and** the shared, non-YARP host-matching layer that the sibling regex items
(`add-regex-hosts`, `add-regex-virtual-path`) will reuse.

## What Changes
- **Core**: a pure `HostPattern` classifier/matcher (exact · leading wildcard `*.suffix` · trailing wildcard
  `prefix.*`) with nginx precedence (exact > leading > trailing). `RouteMatcher` uses it, gaining a
  trailing-wildcard tier.
- **Proxy routing (YARP extension point)**: host forms YARP cannot match natively are carried as
  `RouteConfig.Metadata` and enforced by a custom `DockYarpHostMatcherPolicy` (`MatcherPolicy` +
  `IEndpointSelectorPolicy`) that invalidates candidate endpoints whose request `Host` does not match — the same
  extension ASP.NET Core's own host matching uses. Native exact/leading-wildcard routes keep using
  `Match.Hosts`, so they remain more specific and win.
- `YarpConfigMapper` sets the host metadata and relaxes `Match.Hosts` only for non-native forms (here: trailing
  wildcard).

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `proxy-routing`: a `VIRTUAL_HOST` may be a trailing wildcard (`foo.bar.*`), matched via a YARP endpoint-selector
  policy, with exact and leading-wildcard hosts taking precedence.

## Impact
- **Code**: `DockYarp.Core` (`HostPattern`, `RouteMatcher`), `DockYarp.App` (`YarpConfigMapper` metadata +
  `DockYarpHostMatcherPolicy` + DI registration).
- **Tests**: `DockYarp.Core.Tests` (`HostPattern`, `RouteMatcher` trailing + precedence), `DockYarp.App`/
  integration (`DockYarpHostMatcherPolicy` invalidates a non-matching candidate, keeps a matching one).
- **Foundation**: the metadata channel, the `MatcherPolicy`, and the `HostPattern` matcher are reused by
  `add-regex-hosts` (regex form + ReDoS guards) and `add-regex-virtual-path`.
- **Owning agent**: AG-RP. Resolves `add-trailing-wildcard-hosts`.
