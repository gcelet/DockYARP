# Design — add-regex-hosts

## Regex in `HostPattern`
A `~`-prefixed pattern classifies as `HostPatternKind.Regex`; the body (after `~`) is compiled to a `Regex`:
- `RegexOptions.Compiled | RegexOptions.CultureInvariant`, with a **`matchTimeout`** (100 ms) — the standard
  ReDoS mitigation; `IsMatch` throws `RegexMatchTimeoutException` on catastrophic backtracking, which `Matches`
  catches and treats as **no match** (fail closed).
- An **invalid** regex (compile throws) yields a pattern that never matches, so a bad `VIRTUAL_HOST` can never
  crash discovery or the request path.

### Caching (essential)
`HostPattern.Parse` memoizes results in a static `ConcurrentDictionary<string, HostPattern>` keyed by the raw
pattern. This matters because `DockYarpHostMatcherPolicy.ApplyAsync` parses the route's metadata pattern **per
request**; caching turns that into a dictionary lookup instead of recompiling the regex each time. The cache is
bounded by the number of distinct configured patterns.

## Unified host metadata key
`add-trailing-wildcard-hosts` used `DockYarp.HostTrailing`. Both trailing and regex are "non-native host forms
carrying a raw pattern", so the key is unified to **`DockYarp.HostPattern`** (the raw pattern string).
`DockYarpHostMatcherPolicy` reads that one key and delegates to `HostPattern.Parse(value).Matches(host)`, so it
needs no per-form branching. This is an internal metadata rename (no user-facing surface).

## Precedence (exact > leading > trailing > regex)
- `RouteMatcher` adds a regex tier after trailing; `HostPatternKind` order encodes the precedence.
- YARP: regex routes are non-native (no `Match.Hosts`, filtered by the policy). To keep wildcard > regex when a
  host matches both, `YarpConfigMapper` gives regex routes a lower `Order` (higher number) than trailing routes;
  native exact/leading routes keep `Match.Hosts` and remain most specific.

## Testing
- `HostPattern`: a regex matches the intended hosts and rejects others; an invalid regex never matches. (A
  match-timeout is not unit-tested — it would require a pathological pattern/input and be slow/flaky; the
  fail-closed catch is straightforward.)
- `RouteMatcher`: a regex route matches; exact/leading/trailing win over regex.
- `DockYarpHostMatcherPolicy`: a regex-metadata candidate is invalidated for a non-matching host and kept for a
  matching one (reusing the foundation's test approach).
