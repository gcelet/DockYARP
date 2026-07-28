---
id: add-regex-virtual-path
capability: yarp-dynamic-config
agent: AG-RP
tier: A-structural
priority: low
status: backlog
nginx-proxy: VIRTUAL_PATH (regex location, ~^/(app1|alt1)/)
provenance: split from add-virtual-dest-rewrite (arbitrary VIRTUAL_DEST rewrite shipped there)
---

## Why
nginx-proxy allows a `VIRTUAL_PATH` to be a **regex location** (`~^/(app1|alt1)/`). DockYarp maps `VIRTUAL_PATH`
to a YARP route-template `Path`, which is not a regex, so a `~`-prefixed path is placed into the template
literally and simply never matches.

## nginx-proxy behavior
- A `VIRTUAL_PATH` may be an nginx regex location. nginx-proxy documents that a regex `VIRTUAL_PATH` is **not
  compatible** with `VIRTUAL_DEST` (no prefix rewrite for a regex location).

## DockYarp today
- `VIRTUAL_PATH` becomes `RouteRule.PathPrefix` → YARP `Match.Path = "{prefix}/{**catch-all}"`. YARP path
  matching uses ASP.NET route templates, not regex, so a regex path never matches (silently).
- The arbitrary-rewrite sibling `add-virtual-dest-rewrite` handles only literal `VIRTUAL_PATH` + `VIRTUAL_DEST`.

## Proposed change (sketch)
- Add regex path matching via a **custom, non-YARP matching layer** (custom `MatcherPolicy` or a middleware
  resolving path → route), shared with `add-regex-hosts`/`add-trailing-wildcard-hosts`.
- Compile the regex once (cached, `RegexOptions.Compiled | CultureInvariant`) with a bounded match timeout
  (ReDoS guard); never compile per request.
- Enforce nginx's rule: reject/warn a regex `VIRTUAL_PATH` combined with `VIRTUAL_DEST` (rewrite is undefined
  for a regex location), or document the chosen behavior.

## Acceptance criteria (→ scenarios)
- **WHEN** `VIRTUAL_PATH=~^/(app1|alt1)/` and a request path matches the regex **THEN** it routes to that vhost;
  a non-matching path does not.
- **WHEN** a regex `VIRTUAL_PATH` is combined with `VIRTUAL_DEST` **THEN** the label is rejected with a warning
  (parity with nginx-proxy's incompatibility).
- **WHEN** a regex evaluation exceeds the match timeout **THEN** it fails closed without stalling the request path.

## Notes / risks / references
- Needs the custom matching layer (shared with `add-regex-hosts`, `add-trailing-wildcard-hosts`); YARP path
  matching is route-template only.
- ReDoS is the main risk: bounded match timeout + compiled+cached regex; no per-request compilation.
- Sibling (done): `add-virtual-dest-rewrite` (arbitrary literal `VIRTUAL_DEST` rewrite).
