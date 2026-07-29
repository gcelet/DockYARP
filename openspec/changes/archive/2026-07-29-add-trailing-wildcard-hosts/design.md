# Design — add-trailing-wildcard-hosts (foundation for custom host/path matching)

## Shared host matcher (Core)
`HostPattern` classifies a pattern and matches a host:
- **Exact** — ordinal/case-insensitive equality.
- **Leading wildcard** `*.suffix` — host ends with `.suffix` (any depth).
- **Trailing wildcard** `prefix.*` — host starts with `prefix.` (any suffix).

It exposes the kind (for precedence) and a `Matches(host)` predicate. It is pure and allocation-light so it can
be shared by `RouteMatcher` (DockYarp's internal lookup) and the YARP mapper, and unit-tested directly.
`add-regex-hosts` adds a `Regex` kind here (compiled, cached, ReDoS-bounded).

## Two matchers, one source of truth
DockYarp has two host matchers, and both must agree:
- **YARP** performs the real proxy routing. Native forms (exact, leading `*.`) stay in `Match.Hosts`.
  Non-native forms (trailing wildcard) cannot go in `Match.Hosts`, so the mapper omits `Hosts`, sets a
  catch-all `Path`, and records the pattern in `RouteConfig.Metadata` (`DockYarp.HostTrailing`). A
  `DockYarpHostMatcherPolicy` then filters candidates.
- **`RouteMatcher`** is DockYarp's own lookup used by the security middleware to resolve a request's route for
  per-route policy (auth, client-cert, body size, network access). It gains the trailing tier via `HostPattern`.

Both consult `HostPattern`, so their behavior stays identical.

## YARP endpoint-selector policy
`DockYarpHostMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy`:
- `AppliesToEndpoints`: true if any endpoint's `RouteModel.Config.Metadata` carries a `DockYarp.Host*` key.
- `ApplyAsync`: for each candidate carrying such metadata, evaluate `HostPattern.Matches(request host)`; if it
  does not match, `candidates.SetValidity(i, false)`. Candidates without the metadata are untouched.
- `Order`: runs after ASP.NET's built-in `HostMatcherPolicy` so native host routes are resolved first.

Because non-native routes omit `Hosts`, they are less specific than any explicit-host route, so an exact or
leading-wildcard match is preferred — matching nginx's exact > wildcard precedence. Among trailing-wildcard
routes, `RouteConfig.Order` (from `DOCKYARP_PRIORITY`) breaks ties, consistent with the rest of routing.

## Reading YARP route metadata
The policy reads `endpoint.Metadata.GetMetadata<RouteModel>()?.Config.Metadata`. YARP attaches `RouteModel` to
each proxy endpoint (the same model exposed as `IReverseProxyFeature.Route`), so no reflection or private API is
needed.

## Testing
- `HostPattern`: exact/leading/trailing matching and classification (unit).
- `RouteMatcher`: a trailing-wildcard route matches; exact and leading-wildcard win over trailing (unit).
- `DockYarpHostMatcherPolicy`: `AppliesToEndpoints` detects the metadata; `ApplyAsync` invalidates a
  non-matching candidate and keeps a matching one (constructed endpoints + `CandidateSet`).
