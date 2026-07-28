---
id: add-trailing-wildcard-hosts
capability: proxy-routing
agent: AG-RP
tier: A-structural
priority: low
status: backlog
nginx-proxy: VIRTUAL_HOST (trailing wildcard, e.g. foo.bar.*)
provenance: split from add-wildcard-regex-hosts (leading multi-level wildcard already works; clarified in clarify-multilevel-wildcard-hosts)
---

## Why
nginx-proxy matches a **trailing** wildcard `VIRTUAL_HOST` such as `foo.bar.*` (match any TLD/suffix). DockYarp
supports only a **leading** `*.suffix` wildcard, so a trailing form is parsed as an exact host and never matches.

## nginx-proxy behavior
- Wildcard hostnames include the trailing form `foo.bar.*` (docs "Wildcard Hosts"), alongside the leading
  `*.bar.com`.

## DockYarp today
- `RouteMatcher` (`src/DockYarp.Core/Routing/RouteMatcher.cs`) treats only `*.`-prefixed patterns as wildcards
  (suffix match); a `foo.bar.*` pattern falls into the exact-host index and never matches a real host.
- YARP's `Match.Hosts` supports only a **leading** `*.` wildcard (plus ports) — no trailing wildcard — so this
  cannot map to YARP's native host matcher.

## Proposed change (sketch)
- Extend `RouteMatcher` with a trailing-wildcard tier (prefix match: `host.StartsWith("foo.bar.")`).
- Because YARP cannot express it, route trailing-wildcard hosts through a **custom, non-YARP matching layer**
  (a custom ASP.NET `MatcherPolicy`, or a middleware that resolves the host to a cluster via `RouteMatcher`).
  This is the same infrastructure `add-regex-hosts` needs; consider building them together.
- Preserve precedence: exact > leading wildcard > trailing wildcard (define the tie-break vs regex).

## Acceptance criteria (→ scenarios)
- **WHEN** `VIRTUAL_HOST=foo.bar.*` and a request targets `foo.bar.com` **THEN** it routes to that vhost.
- **WHEN** an exact host and a matching trailing wildcard both exist **THEN** the exact host wins.

## Notes / risks / references
- Needs the custom host-matching layer (shared with `add-regex-hosts`); YARP host matching is leading-`*.` only.
- Sibling (done): `clarify-multilevel-wildcard-hosts` (leading multi-level wildcard confirmed working).
