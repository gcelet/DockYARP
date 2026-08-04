## Why
The site now has the full reference (labels/env, application config, runtime features), but a new user still
has to assemble it themselves. Task-oriented, copy-pasteable **recipes** are the fastest way to get a working
configuration for a common scenario.

## What Changes
- Add an **Examples** page (`docs-site/content/en/docs/examples.md`) with worked recipes, each a real
  Compose snippet + the expected result, reusing a shown-once base stack (`dockyarp` + read-only socket proxy):
  basic virtual host, path routing + rewrite, multiple ports, configuration via environment variables,
  automatic HTTPS (Let's Encrypt), mutual TLS, a per-host TLS policy, Basic Auth, internal-only access, and
  running behind a load balancer.
- Cross-link Getting Started / Configuration / Features to it; place it after the reference pages.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `documentation`: the site provides worked configuration recipes for common scenarios.

## Impact
- **Docs**: new `docs-site/content/en/docs/examples.md` (+ weight renumber of the pages after it, + a cross-link)
  — content only. No code, no build gate.
- **Spec**: `documentation` — ADDED requirement for the worked examples.
- **Owning agent**: AG-DOC. Resolves `add-doc-examples`. Last of the documentation-content follow-ups.
