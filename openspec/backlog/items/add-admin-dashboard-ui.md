---
id: add-admin-dashboard-ui
capability: admin-api
agent: AG-AA
tier: B-runtime
priority: low
nginx-proxy: n/a (DockYarp value-add — nginx-proxy has no admin UI either)
provenance: 2026-08-16 user backlog discussion (Aspire-dashboard-inspired admin UI)
status: backlog
---

## Why
The Admin API (`AdminEndpoints.cs`) is **JSON-only**: `/api/{version,routes,clusters,certs,resolve,health}` +
Prometheus `/metrics`. Getting a "what's running right now" picture means curling endpoints or wiring Grafana.
The user wants a **light** HTML dashboard on the admin surface that gives the same kind of at-a-glance macro
view Aspire's local dev dashboard gives for a distributed app — resources + their state, not deep observability.

## Current state
- `AdminEndpoints.MapAdminApi` maps `/api/*` behind `ApiKeyEndpointFilter`, optionally scoped to a dedicated
  `AdminApi:Host` (`isolate-admin-api`, `add-admin-host-cert`). No static assets / view layer exist in
  `DockYarp.AdminApi` or `DockYarp.App` today — no npm/frontend build step anywhere in the App project.
- Data already available for a dashboard with **no new aggregation logic**:
  - `/api/routes`, `/api/clusters` — current `RouteConfigSnapshot` (hosts, paths, clusters, destinations).
  - `/api/certs` (`ICertificateInventory.List()`) — per-host cert state/expiry.
  - `/api/health` (`IDiscoveryHealth`) — overall status (`Healthy`/`Degraded`) + discovery connected/disconnected
    + route/cluster/cert counts.
  - Prometheus `/metrics` (`DockYarpMetrics`) exists for real observability/alerting already — the dashboard is
    NOT meant to replace it.

## Proposed change (sketch) — revised 2026-08-16 (server-rendered, no API key in the browser)
**Architecture correction from the first sketch**: client-side JS polling the `ApiKeyEndpointFilter`-gated
`/api/*` endpoints would require embedding the admin API key in browser-visible JS/HTML — a credential leak.
DockYarp is an ASP.NET Core app already (`DockYarp.App`); staying consistent with that stack, the dashboard is
**server-rendered** (Razor Pages, or a small MVC area) — no HTTP round-trip to `/api/*` from the browser at all.
- The page(s) read directly from the same interfaces the Admin API endpoints already use
  (`IRouteConfigStore`, `ICertificateInventory`, `IDiscoveryHealth`) via DI, in-process — no key material ever
  reaches the client.
- Aspire-like **resource grid**, scaled down to what DockYarp actually has:
  - **Resources table**: one row per route (host + path) → cluster → destinations, with a state dot/badge.
  - **Certificates panel**: host, issuer, expiry countdown; flag near-expiry.
  - **Status banner**: overall Healthy/Degraded + discovery connected/disconnected.
- **Auto-refresh**: if a live-updating feel is wanted (vs. plain reload/`<meta refresh>`), consider **htmx**
  for partial-page swaps — sober, no SPA framework, no build step, vendor the single JS file locally (no CDN,
  keeps the "light" budget) — over hand-rolled vanilla `fetch`, since htmx's polling (`hx-trigger="every Ns"`)
  hits **new Razor Page/MVC handler endpoints** (server-rendered HTML fragments), not the JSON `/api/*` group,
  so the same "no key in the browser" property holds for refreshes too.
- **Open decision (resolve at propose-time, not here): how the dashboard page itself is protected.** It cannot
  reuse `ApiKeyEndpointFilter` as-is (that's an API-key header check, not something a browser navigating to a
  page can supply without exposing the key in a URL/cookie/JS anyway). Options to weigh: (a) a login form that
  accepts the existing API key server-side once and sets an HttpOnly+Secure session cookie (key still never
  touches JS); (b) piggyback on `AdminApi:Host` isolation + treat the dashboard as trusted-network-only with no
  extra auth (weaker, only viable if the admin host is never internet-exposed); (c) a separate
  dashboard-specific credential. Do not default to (b) without the user confirming the threat model.
- **Explicitly out of scope for v1**: logs / traces / metrics explorer (Aspire has these; DockYarp already
  exports OTEL/Prometheus for that job — the dashboard stays a *macro* view, not a debugger).

## Acceptance criteria (→ scenarios)
- **WHEN** an operator opens the dashboard on the admin host **THEN** they see a resource table of current
  routes/clusters with a health state, refreshing without a manual page reload.
- **WHEN** a certificate is near expiry or discovery is disconnected **THEN** it is visually flagged (not just
  buried in JSON).
- **WHEN** `AdminApi:Host` is unset (all-hosts / dev) **THEN** the dashboard is still reachable, under whatever
  auth mechanism gets decided at propose-time (see the open decision above).
- **WHEN** the page loads **THEN** no admin API key (or any credential) is present in the HTML/JS delivered to
  the browser, and the page ships with no external CDN/framework and stays small — "light" is a hard
  requirement, not an aspiration.

## Notes / risks / references
- Decide at propose-time: **Razor Pages vs. a minimal MVC area** for the view layer (Razor Pages is likely the
  better fit — no controller ceremony needed for a handful of read-only pages); and the dashboard auth
  mechanism (see open decision above). Avoid introducing any frontend build tooling — none exists in
  `DockYarp.App` today and this should not be the item that adds one; htmx, if used, is vendored as a single
  local JS file, not pulled from a CDN.
- Path collision: `/api/*` reserved paths already documented (`examples.md` warning from `isolate-admin-api`);
  a top-level `/dashboard` (or admin-host root) avoids that, but confirm the exact path at propose-time.
- Pairs conceptually with the still-not-a-stub **dedicated admin PORT** follow-up
  ([[backlog-work-queue]] section F) — if/when that lands, the dashboard moves with the admin host.
- No parity row: nginx-proxy has no equivalent admin UI, so this is a pure DockYarp value-add, not a gap.
- Refs: `src/DockYarp.AdminApi/AdminEndpoints.cs`, `AdminApiModels.cs`, `ICertificateInventory.cs`,
  `IDiscoveryHealth.cs`, `DockYarpMetrics.cs`.
