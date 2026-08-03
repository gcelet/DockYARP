## Why
The docs site's Configuration page and the in-repo `docs/labels-reference.md` list only a starter subset of the
container configuration surface and predate the env-var work — they omit `SSL_POLICY`, `SERVER_TOKENS`,
`CERT_NAME`, `NETWORK_ACCESS`, `EXTERNAL_HTTPS_PORT`, the per-host `ENABLE_HTTP_ON_MISSING_CERT`/
`TRUST_DEFAULT_CERT` overrides, the nginx-proxy namespaced label aliases, and the fact that any key can be set
as an **environment variable** (env wins over label). A user migrating from nginx-proxy cannot see the real,
current container-configuration reference.

## What Changes
- **Bounded scope: the container-configuration surface** (labels + environment variables). Bring both the site's
  `docs-site/content/en/docs/configuration.md` and the in-repo `docs/labels-reference.md` fully current: every
  recognized key, grouped by area (routing, TLS, access control & tuning), with behavior, default, and a real
  example; the env-or-label channel (env wins) stated up front; the namespaced label aliases noted.
- Out of scope (→ follow-up items just added): the per-capability **application-config** reference
  (`add-doc-capability-reference`) and worked **examples/recipes** (`add-doc-examples`).

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `documentation`: the site documents the full container label/environment-variable configuration reference.

## Impact
- **Docs**: `docs-site/content/en/docs/configuration.md` (site) and `docs/labels-reference.md` (in-repo) —
  content only. No code, no build gate.
- **Spec**: `documentation` — ADDED requirement for the configuration reference (labels + env vars).
- **Follow-ups created**: `add-doc-capability-reference` (audit + app-config/runtime reference),
  `add-doc-examples` (recipes).
- **Owning agent**: AG-DOC. Resolves the container-config part of `add-doc-feature-reference`.
