---
id: add-regex-hosts
capability: proxy-routing
agent: AG-RP
tier: A-structural
priority: medium
status: backlog
nginx-proxy: VIRTUAL_HOST (regex form, ~^…$)
provenance: split from add-wildcard-regex-hosts (leading multi-level wildcard already works; clarified in clarify-multilevel-wildcard-hosts)
---

## Why
nginx-proxy treats a `VIRTUAL_HOST` starting with `~` as an nginx `server_name` **regex**
(`~^app-\d+\.example\.com$`), commonly used for nip.io/sslip.io-style dynamic hosts. DockYarp has no regex host
matching, so such a value is parsed as an exact host and never matches.

## nginx-proxy behavior
- A `VIRTUAL_HOST` beginning with `~` is a regex `server_name`; regex hosts always get SHA1 upstream names, and
  precedence follows nginx specificity: exact > wildcard > regex.

## DockYarp today
- `RouteMatcher` supports exact + leading-`*.` wildcard only; a `~…` value never matches.
- YARP's `Match.Hosts` has **no** regex support (leading `*.` wildcard + ports only).

## Proposed change (sketch)
- Add a regex tier to `RouteMatcher`: a `~`-prefixed host compiles to a `Regex`
  (`RegexOptions.Compiled | CultureInvariant`), cached, evaluated after exact + wildcard tiers.
- Route regex hosts through a **custom, non-YARP matching layer** (custom `MatcherPolicy` or a middleware that
  resolves host → cluster via `RouteMatcher`), since YARP cannot express it. Shared with
  `add-trailing-wildcard-hosts`.
- **ReDoS safety**: compile with a bounded `matchTimeout`, validate/deny catastrophic patterns, and never
  compile per request (cache by pattern). Consider capping the number of regex hosts.
- Preserve precedence: exact > wildcard > regex.

## Acceptance criteria (→ scenarios)
- **WHEN** `VIRTUAL_HOST=~^app-\d+\.example\.com$` and the request host matches **THEN** it routes; a
  non-matching host does not.
- **WHEN** an exact or wildcard host also matches **THEN** it wins over the regex (specificity order).
- **WHEN** a regex evaluation exceeds the match timeout **THEN** it fails closed (no match) without stalling the
  request path.

## Notes / risks / references
- Needs the custom host-matching layer (shared with `add-trailing-wildcard-hosts`); YARP host matching is
  leading-`*.` only.
- ReDoS is the main risk: bounded match timeout + compiled+cached regex; no per-request compilation.
- Sibling (done): `clarify-multilevel-wildcard-hosts`.
