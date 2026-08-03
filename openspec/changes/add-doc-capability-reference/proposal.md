## Why
`add-doc-feature-reference` documented the container label/env surface. The site still describes the proxy's own
**application configuration** only as a five-bullet summary — users cannot see the actual sections, keys,
defaults, and how to set them (`appsettings.json` vs `Foo__Bar` env vars).

## What Changes
- **Bounded scope: the application-configuration reference.** Expand the site's Configuration page
  "Application configuration" section into a complete per-section reference — `Server`, `Docker`, `Tls`,
  `Security`, `Routing`, `Proxy`, `AccessLog`, `AdminApi`, `Compression`, `DataProtection`, `Host` — each key
  with its default and purpose, and a note that any key may be set via `appsettings.json` or a double-underscore
  environment variable (e.g. `Tls__AcceptTermsOfService=true`).
- Out of scope (→ new follow-up `add-doc-runtime-reference`): the whole-site audit against `openspec/specs/`
  and the runtime-feature narrative (admin API endpoints, access-log fields, host/multi-network discovery,
  container filters, LB policies, error pages, static config).

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `documentation`: the site documents the proxy's application configuration (sections, keys, defaults, and the
  appsettings/env-var channel).

## Impact
- **Docs**: `docs-site/content/en/docs/configuration.md` (the "Application configuration" section) — content only.
  No code, no build gate.
- **Spec**: `documentation` — ADDED requirement for the application-configuration reference.
- **Follow-up created**: `add-doc-runtime-reference` (site audit + runtime-feature narrative).
- **Owning agent**: AG-DOC. Resolves the application-config part of `add-doc-capability-reference`.
