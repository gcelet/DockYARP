---
id: add-vhost-config-overrides
capability: yarp-dynamic-config
agent: AG-RP
tier: B-runtime
priority: medium
status: backlog
nginx-proxy: vhost.d/<host>[_location][_override], conf.d
provenance: this parity pass (matrix: vhost.d ⛔)
---

## Why
nginx-proxy's biggest extensibility feature is mounted config snippets: per-vhost (`vhost.d/<host>`), global
(`vhost.d/default`), per-location (`_location`), and full override (`_location_override`), plus proxy-wide
`conf.d`. This is how operators add headers, rewrites, auth, size limits, etc. DockYarp has no raw
config-injection escape hatch, so anything not modeled as a label/option is impossible.

## nginx-proxy behavior
- `vhost.d/<host>` (server-block), `vhost.d/default` (global), `<host>_location` / `default_location`
  (inside `location`), and `<host>[_<pathhash>]_location_override` (replace the generated location entirely).
  `conf.d/*.conf` for proxy-wide directives (`test_custom`, `test_location-override`).

## DockYarp today
Routes/clusters/transforms are generated from labels + static JSON config
(`src/DockYarp.App/ReverseProxy/`, `StaticConfig/`); there is no per-vhost raw-config injection or override
(matrix ⛔). Since DockYarp is YARP, "raw nginx config" has no literal equivalent — the analog is structured
per-route overrides.

## Proposed change (sketch)
Define a **structured** per-host/global override source (extend the static JSON config): additional response
headers, extra transforms, metadata, and a "replace generated route" mode — the YARP-native analog of
`vhost.d`. Merge with discovery output at a defined precedence (override wins). Do not attempt to interpret
nginx syntax.

## Acceptance criteria (→ scenarios)
- **WHEN** a per-host override adds a response header **THEN** responses for that host carry it.
- **WHEN** a global (default) override is set **THEN** it applies to hosts without a specific override.
- **WHEN** an override replaces a generated route **THEN** the override's route definition is used instead.

## Notes / risks / references
- Scope carefully — this can sprawl. Start with headers + transforms + replace-route; document that it is
  structured (not raw nginx).
