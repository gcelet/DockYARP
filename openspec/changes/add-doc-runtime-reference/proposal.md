## Why
The site now documents the **configuration** surface (container labels/env + application config) but not the
**runtime behavior**: how DockYARP discovers containers, routes and load-balances, manages TLS, exposes
observability (`/metrics`, access logs) and the admin API, applies static config and error pages, and shuts
down. An audit of the site against `openspec/specs/` shows this runtime narrative is the remaining content gap.

## What Changes
- Add a **Runtime features** reference page (`docs-site/content/en/docs/features.md`) documenting, with behavior
  and examples where useful:
  - **Discovery** — labels/env, health-aware exclusion, network selection (`PreferredNetwork`/`ProxyNetworks`/
    `HostAddress`), `Docker:ContainerFilters`, live event-driven reconciliation.
  - **Routing & load balancing** — host/path matching, exact-beats-wildcard precedence, priority, multiport,
    `VIRTUAL_DEST` path rewrite, default host + fallback response, the LB policies.
  - **TLS/ACME** — automatic issuance + renewal, SNI selection, self-signed fallback, provided certs, `CERT_NAME`,
    per-host `SSL_POLICY`, mutual TLS, HTTP→HTTPS enforcement (cross-links to Configuration).
  - **Observability** — the `/metrics` Prometheus endpoint and the structured access log (field catalog,
    exclusions, `AccessLog:Fields`).
  - **Admin API** — `/api/routes`, `/api/clusters`, `/api/certs`, `/api/health`, `/api/resolve`, protected by the
    `X-Api-Key` header (`AdminApi:ApiKey`).
  - **Static configuration** (`StaticConfig:Path`), **custom error pages** (`ErrorPages:Directory`), and
    **graceful shutdown** (`Host:ShutdownTimeoutSeconds`).
- Cross-link the existing pages to it; spot-check them for obvious staleness.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `documentation`: the site documents DockYARP's runtime features (discovery, routing/LB, TLS, observability,
  admin API, static config, error pages, shutdown).

## Impact
- **Docs**: new `docs-site/content/en/docs/features.md` (+ light cross-linking) — content only. No code, no gate.
- **Spec**: `documentation` — ADDED requirement for the runtime feature reference.
- **Owning agent**: AG-DOC. Resolves `add-doc-runtime-reference`. Completes the site reference trilogy
  (labels/env → app-config → runtime). Worked recipes remain (`add-doc-examples`).
