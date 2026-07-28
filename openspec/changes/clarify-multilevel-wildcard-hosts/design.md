# Design — clarify-multilevel-wildcard-hosts

## Finding: multi-level leading wildcards already work
Two independent host matchers agree on multi-level leading wildcards:
- **`RouteMatcher`** (used by the security middleware for per-route lookup) matches a wildcard by suffix:
  `host.Length > suffix.Length && host.EndsWith(suffix)` where `*.local` → suffix `.local`. That matches
  `app.local` and `a.b.local` alike.
- **YARP** (the actual proxy router) maps `HostPattern` straight into `RouteMatch.Hosts`, and YARP's wildcard is
  documented as multi-level: `*.domain.com` matches `www.subdomain.domain.com`.

So no code change is needed; this change pins the behavior with tests and corrects the parity matrix, which had
recorded it as "single-level only".

## Why trailing wildcards and regex are split out
YARP's `Match.Hosts` supports only a **leading** `*.` wildcard (plus ports). It has no trailing-wildcard
(`foo.bar.*`) and no regex host matching. Supporting either means resolving the host **outside** YARP's native
matcher — e.g. a custom ASP.NET `MatcherPolicy`, or a middleware that maps the request host to a cluster via an
extended `RouteMatcher` — and, for regex, a compiled-and-cached `Regex` with ReDoS guards. That is a real
architectural addition, so each ships as its own backlog item (`add-trailing-wildcard-hosts`, `add-regex-hosts`)
rather than being bundled into this clarification.

## Precedence unchanged
Existing precedence (exact host > wildcard, then priority, then longest path) is untouched; the added scenario
only asserts that a nested subdomain matches the wildcard tier.
