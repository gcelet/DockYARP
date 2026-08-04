## Why
A site-vs-`openspec/specs/` audit (2026-08-04) found shipped features absent from the site or present only as a
config-table row. The `documentation` "Runtime feature reference" requirement enumerates the documented runtime
features, and several of these were not in that list — so documenting them extends the requirement.

## What Changes
- Document the missing features on the site: **file-based Basic Auth (htpasswd)**, **regex `VIRTUAL_PATH`**,
  **per-host static-config `Overrides`**, **httpoxy `Proxy`-header stripping**, and **response compression** —
  in `features.md` (new Access control + Proxying sections, a regex bullet, the static-config `Overrides`
  sample), `configuration.md` (the regex form on `VIRTUAL_PATH` — fulfilling the open-ended container-config
  requirement, no delta), and an htpasswd recipe in `examples.md`.
- **MODIFY** the `documentation` "Runtime feature reference" requirement to enumerate access control, response
  compression + httpoxy stripping, and the per-host static-config overrides now covered by the site.

## Capabilities
### Modified Capabilities
- `documentation`: the runtime feature reference now also covers access control (Basic Auth incl. htpasswd,
  internal-only), response compression, httpoxy `Proxy`-header stripping, and per-host static-config overrides.

## Impact
- **Docs**: `docs-site/content/en/docs/features.md` / `configuration.md` / `examples.md` — content only. No code.
- **Spec**: `documentation` — MODIFIED "Runtime feature reference".
- **Owning agent**: AG-DOC. Resolves `add-doc-feature-gaps`; the site now fully reflects the shipped features.
