## Why

The e2e `Priority_HigherWins` scenario was unsound. It was first written with two containers on the same
`VIRTUAL_HOST` (aggregated into one round-robin cluster, so the answer was random), then reworked to a
wildcard-vs-exact host where the higher-priority **wildcard** was expected to win. A real e2e run disproved
that: the **exact host wins** even against a higher-priority wildcard.

That is exactly the intended, nginx-matching behaviour, confirmed in the code: DockYarp's `RouteMatcher`
selects **exact hosts before wildcards** (host specificity first), and YARP does the same. nginx-proxy (via
nginx `server_name`) also prefers an exact host over a wildcard and has **no priority concept**. So:

- Host selection (exact over wildcard) already matches nginx-proxy — there is nothing to converge.
- `DOCKYARP_PRIORITY` is a **DockYarp extension** (no nginx-proxy equivalent). It orders routes **within
  the same host**; it cannot — and must not — override host specificity. It is therefore not demonstrable
  through label-based route competition (same-host containers are aggregated), so it does not belong in the
  e2e suite.

The `proxy-routing` requirement text ("the matching route with the highest priority, preferring an exact host
match over a wildcard…") reads as *priority-first*, which is misleading: the actual precedence is host
specificity first, then priority/path within a host.

## What Changes

- **Remove the e2e priority scenario** (`Priority_HigherWins` and its backends). Priority stays covered by the
  in-process unit tests (`docker-discovery` label parsing, `yarp-dynamic-config` priority→order mapping).
- **Clarify the `proxy-routing` "Host and path matching" requirement**: precedence is host specificity first
  (exact over wildcard, as in nginx-proxy), then, among routes for the same host, highest priority then
  longest path. Add a scenario making explicit that an exact host wins even over a higher-priority wildcard.
- **Document** in the docs: host/path selection matches nginx-proxy; `DOCKYARP_PRIORITY` is a DockYarp
  extension with no nginx-proxy equivalent.

No product code change — the behaviour already matches nginx-proxy for host/path selection.

## Capabilities

### Modified Capabilities
- `proxy-routing`: the route-selection precedence is clarified (host specificity before priority); no
  behaviour change.

## Impact

- **Docs/tests only**: `tests/DockYarp.E2E.AppHost/BackendCatalog.cs`,
  `tests/DockYarp.E2E.Tests/RoutingTests.cs`, `docs/architecture.md`, `docs/labels-reference.md`. No `src/`
  change.
- **Owning agent**: AG-RP (with AG-DEP).
