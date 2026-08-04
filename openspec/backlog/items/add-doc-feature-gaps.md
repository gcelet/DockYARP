---
id: add-doc-feature-gaps
capability: documentation
agent: AG-DOC
tier: C-doc
priority: medium
status: backlog
nginx-proxy: (internal — doc audit 2026-08-04)
provenance: 2026-08-04 site-vs-specs audit — shipped features missing/thin on the site
---

## Why
A site-vs-`openspec/specs/` audit found shipped features that are absent from the site or only present as a
config-table row. They must be documented so the site fully reflects what DockYARP does.

## Scope — the confirmed gaps
- **File-based Basic Auth (htpasswd)** (`Security:HtpasswdDirectory`, per-vhost `<host>` / per-path
  `<host>_<sha1(path)>` files, hot-reloaded) — document as a feature + a recipe, distinct from the label-based
  `DOCKYARP_AUTH_*`.
- **Regex path matching** (proxy-routing) — document how a regex `VIRTUAL_PATH` matches (verify exact syntax
  against the code/spec before writing).
- **Per-host configuration overrides** (`vhost.d`-style, yarp-dynamic-config) — document what can be overridden
  per host/global (verify behavior against the code/spec).
- **Strip the inbound `Proxy` header** (httpoxy mitigation, yarp-dynamic-config) — a brief security note on the
  Features page (it is automatic, not user-configurable).
- **Response compression** (gzip/brotli, `Compression:Enabled`) — call it out on the Features page (currently
  only in the app-config table).

## Acceptance criteria (→ scenarios)
- **WHEN** the site is re-audited against `openspec/specs/` **THEN** each of the above is documented with
  behavior (and an example where it is user-configured).

## Notes / risks / references
- Follows the reference trilogy + examples (all done). Verify each feature's exact behavior against the code
  before documenting (some were archived a while ago). Keep in sync via the "document as you go" lifecycle rule.
