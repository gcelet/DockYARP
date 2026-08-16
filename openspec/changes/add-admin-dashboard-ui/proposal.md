## Why

The Admin API (`AdminEndpoints.cs`) is JSON-only: `/api/{version,routes,clusters,certs,resolve,health}` +
Prometheus `/metrics`. Getting a "what's running right now" picture means curling endpoints or wiring Grafana.
The user wants a light HTML dashboard on the admin surface giving the same kind of at-a-glance macro view
Aspire's local dev dashboard gives — resources + their state, not deep observability.

**Three decisions resolved with the user before/during this proposal**:
- **Auth**: network isolation only for v1 — the dashboard carries no application-level authentication; the
  documented posture is that `AdminApi:Host` is never internet-exposed (same assumption `isolate-admin-api`
  already documents). OIDC is wanted later but explicitly deferred — tracked separately as
  `add-admin-dashboard-oidc-auth`, not built here. This item's routes are registered through their own
  extension method (not folded into `MapAdminApi`) so an auth requirement can be layered on later.
- **View layer**: Razor Pages (not MVC) — a single page, no controller ceremony needed.
- **Project isolation** (user feedback during implementation): the dashboard is its own project,
  `DockYarp.Dashboard` (a Razor-Pages-enabled class library), not folded into `DockYarp.App`.
  `DockYarp.AdminApi` stays untouched/isolated as it already was. The dashboard must also be **disableable**.

## What Changes

- New project `src/DockYarp.Dashboard/` (`Microsoft.NET.Sdk.Razor`, `AddRazorSupportForMvc`, referencing
  `DockYarp.AdminApi` + `DockYarp.Core`; referenced by `DockYarp.App` the same way every other module is):
  - `Pages/Dashboard/Index.cshtml` (+ `.cshtml.cs`), route pinned to `/dashboard` via `@page "/dashboard"`
    (not folder-convention-derived, so it doesn't depend on `RootDirectory` layout).
  - Reads `IRouteConfigStore`, `ICertificateInventory`, `IDiscoveryHealth` directly via DI — **no HTTP
    round-trip to `/api/*` from the browser**, so no admin API key material ever reaches the client.
  - Content, built from data that's honestly available (checked the actual models before designing the UI —
    see design.md for what was cut): a status banner (Healthy/Degraded, discovery connected/disconnected,
    route/cluster/cert counts — from `HealthView`), a resources table (route → cluster → destinations, from
    `RouteView`/`ClusterView`), a certificates panel (host, expiry, near-expiry flagged — from `CertView`).
    **No per-route/per-destination health indicator** — no such per-resource signal exists anywhere in the
    codebase today (checked); inventing one would show false precision.
  - Self-contained inline CSS, no external CDN, no JS framework.
  - Auto-refresh via `<meta http-equiv="refresh">` for v1 (zero JavaScript) rather than htmx partial-swap —
    see design.md for why.
  - `DashboardServiceCollectionExtensions.AddDockYarpDashboard(this IServiceCollection, AdminApiOptions)` and
    `DashboardEndpointMapping.MapDockYarpDashboard(this WebApplication)`, both gated on a new
    `AdminApiOptions.DashboardEnabled` (default `true`) — when `false`, no Razor Pages services are even
    registered and no route is mapped (not just hidden behind a 404).
- `AdminApiOptions.DashboardEnabled` (bool, default `true`) — the disable toggle.
- Docs: a reserved-path warning for `/dashboard` on the admin host, mirroring the existing `/api/*` warning
  `isolate-admin-api` added to `examples.md`; a `configuration.md`/`features.md` note describing the dashboard,
  `AdminApi:DashboardEnabled`, and its network-isolation-only auth model explicitly (so an operator doesn't
  assume it's API-key protected).

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `admin-api`: adds a **Read-only admin dashboard** requirement (a server-rendered `/dashboard` page, scoped to
  the admin host, no admin API key ever sent to the browser, no per-resource health data fabricated beyond what
  the admin API itself already reports).

## Impact

- New: `src/DockYarp.Dashboard/DockYarp.Dashboard.csproj`, `DashboardServiceCollectionExtensions.cs`,
  `DashboardEndpointMapping.cs`, `Pages/Dashboard/Index.cshtml(.cs)`.
- Modified: `src/DockYarp.App/DockYarp.App.csproj` (new project reference), `src/DockYarp.App/Program.cs`
  (delegates registration to `AddDockYarpDashboard` inside `AddDockYarpObservability`, calls
  `MapDockYarpDashboard`), `src/DockYarp.AdminApi/AdminApiOptions.cs` (`DashboardEnabled`), `DockYarp.slnx`,
  `docs-site/content/en/docs/{configuration,features,examples}.md` (as relevant), `docs/labels-reference.md` if
  applicable (no new label/env var expected — check at apply time).
- Tests: `DockYarp.IntegrationTests` — the page responds 200 on the admin host (or all hosts when unset), is
  absent/falls through on other hosts when `AdminApi:Host` is set (mirroring `isolate-admin-api`'s existing
  test pattern for `/api/*`), and is entirely absent when `AdminApi:DashboardEnabled` is `false`.
