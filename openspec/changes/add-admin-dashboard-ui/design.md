## Context

See `proposal.md` - Why for the auth/view-layer decisions already resolved with the user. Checked before
designing (grounding, not guessing):
- `AdminEndpointMapping.MapDockYarpAdmin` (`src/DockYarp.App/Observability/`) is the existing pattern to mirror:
  reads `AdminApiOptions` from DI, calls `app.MapAdminApi(options.Host)`, and separately `RequireHost`-scopes
  `MapPrometheusScrapingEndpoint()`. The dashboard's mapping follows the same shape.
- `AdminApiModels`/`AdminMapper`/`ICertificateInventory`/`IDiscoveryHealth` (`DockYarp.AdminApi`) are exactly
  what `/api/*` already reads — reusing them in-process is what makes "no HTTP round-trip, no key in the
  browser" possible with zero new aggregation logic.
- Checked the actual data available before sketching the UI: `RouteView`/`ClusterView`/`EndpointView` carry no
  per-resource health flag, and nothing else in the codebase (`ActiveHealthCheckConfig`/`PassiveHealthCheckConfig`
  are YARP-internal, not surfaced to the admin layer) provides one either. Only `HealthView` (overall
  Healthy/Degraded + discovery connected/disconnected + counts) is a real health signal. The original backlog
  sketch's "state dot per resource" was aspirational, not backed by data — dropped, see proposal.md.
- `DockYarp.App.csproj` already uses `Microsoft.NET.Sdk.Web`, so Razor Pages support (`AddRazorPages`,
  `MapRazorPages`) needs no new package reference — it's part of the shared ASP.NET Core framework already
  referenced by the Web SDK.
- `DockYarp.AdminApi` is a plain class library (no view engine, no `Sdk.Web`) — confirms the view layer belongs
  in `DockYarp.App`, not there, matching the module dependency graph (`App` → everything; a library shouldn't
  gain a Razor/view dependency).

## Goals / Non-Goals

**Goals:**
- A single `/dashboard` page, server-rendered, no admin API key reaching the browser, no fabricated data.
- Reuse `AdminEndpointMapping`'s exact scoping pattern so the dashboard's host-isolation behavior is provably
  consistent with `/api/*`'s, not a parallel reimplementation.

**Non-Goals:**
- Any application-level authentication (network isolation only — decided; OIDC deferred to
  `add-admin-dashboard-oidc-auth`).
- htmx / partial-page live updates for v1 (see Decisions) — deferred, not this item.
- A per-resource health indicator — no such data exists (see Context); adding one here would mean either
  fabricating it (dishonest) or building new active probing (out of scope, a much bigger feature).
- A dedicated admin port — still tracked separately in [[backlog-work-queue]] section F, unrelated to this item.

## Decisions

- **`<meta http-equiv="refresh" content="10">`, not htmx, for v1 auto-refresh.** The backlog stub's own
  phrasing was conditional ("if a live-updating feel is wanted... consider htmx"), not a requirement. A meta
  refresh satisfies the acceptance criterion ("refreshing without a manual page reload") with **zero**
  JavaScript — strictly lighter than vendoring htmx, and there's no partial-swap endpoint to build/maintain.
  Revisit only if a full-page reload every 10s turns out to feel too jarring in practice — an easy, isolated
  swap later (the page has no other JS to entangle it with).
- **Route pinned via `@page "/dashboard"` on the page itself**, not inferred from `RootDirectory`/folder
  convention — guarantees the URL regardless of where the `.cshtml` file physically lives, and makes the
  reserved path immediately visible by reading the page file.
- **`DashboardEndpointMapping.MapDockYarpDashboard`, a new extension method, not added to
  `AdminEndpointMapping.MapDockYarpAdmin`.** Two reasons: (1) the auth-decision follow-up
  (`add-admin-dashboard-oidc-auth`) needs its own mapping seam to layer `RequireAuthorization` onto later
  without touching the JSON API's mapping; (2) `MapDockYarpAdmin` lives in the `Observability` folder
  (admin API + metrics) — the dashboard is a distinct concern (a view, not an API), so it gets its own
  `Dashboard/` folder and extension method, called separately (but adjacently) from `Program.cs`.
- **Own project, `DockYarp.Dashboard`, not folded into `DockYarp.App`** (user feedback during implementation —
  the first pass put the Pages directly under `DockYarp.App`). `Microsoft.NET.Sdk.Razor` +
  `<AddRazorSupportForMvc>true</AddRazorSupportForMvc>` (without this, the SDK compiles the `.cshtml` files but
  they never register as routable pages — found by a failing test: `/dashboard` 404'd until this was set,
  confirmed via `RAZORSDK1004`'s warning text pointing at exactly this property) + `<FrameworkReference
  Include="Microsoft.AspNetCore.App" />` (needed since `Sdk.Razor` alone doesn't pull in ASP.NET Core APIs the
  way `Sdk.Web` does). References `DockYarp.AdminApi` + `DockYarp.Core`; `DockYarp.App` references it like every
  other module. `DockYarp.AdminApi` itself is untouched (no dashboard code added there) — the two stay isolated,
  as asked. Referenced-project Razor Pages are auto-discovered by the host's `AddRazorPages()`/`MapRazorPages()`
  with no extra `AddApplicationPart` wiring needed (confirmed by the passing integration tests once
  `AddRazorSupportForMvc` was set).
- **`AdminApiOptions.DashboardEnabled`** (default `true`) is the disable toggle, read in *both*
  `DashboardServiceCollectionExtensions.AddDockYarpDashboard` (skips `AddRazorPages()` entirely when `false` —
  no Razor Pages services registered at all, not just a hidden route) and `DashboardEndpointMapping.MapDockYarpDashboard`
  (skips `MapRazorPages()` to match — calling it without the services registered would throw). Both gates read
  the same option so they can't drift out of sync.
- **Where `AddDockYarpDashboard` is called from**: inside `AddDockYarpObservability`
  (`DockYarp.App/Observability/ObservabilityServiceCollectionExtensions.cs`), not a new top-level
  `Program.cs` statement. `Program.cs`'s top-level statement count is analyzer-capped (`AV1500`, max 40) and was
  already near that ceiling; `AddDockYarpObservability` already binds `AdminApiOptions` for the JSON admin API,
  so passing that same instance one line further to the dashboard's registration avoids both a second bind and
  a new top-level statement, at the cost of `Observability`'s extension now also owning "register the
  dashboard" — an acceptable, narrow coupling given both features already share the same `AdminApiOptions`.
- **Certificates panel flags "near-expiry"** using the same kind of threshold DockYarp's own renewal logic
  already reasons about (a small buffer before `NotAfter`) — a display-only flag, not a new computation on top
  of `CertView.NotAfter` (just a comparison against `DateTimeOffset.UtcNow` at render time).
- **Reserved-path docs**: `/dashboard` on the admin host gets the same treatment `isolate-admin-api` gave
  `/api/*` — a warning in `examples.md` that a backend's own `/dashboard` path (if any) would be shadowed on
  that host. Low collision risk in practice, but the precedent is to document it regardless.

## Risks / Trade-offs

- [No application-level auth — a real security posture, not a placeholder] → this was the explicit, confirmed
  decision (see proposal.md); mitigated by prominent docs stating the dashboard is network-isolation-only, so
  an operator can't assume API-key-equivalent protection.
- [Meta-refresh causes a full page reload, momentarily blanking the view] → acceptable for a macro/glance
  dashboard refreshed every 10s; not the UX bar Aspire's own live dashboard sets, but consistent with "light"
  being a hard requirement over polish.
- [Dropping the per-resource health dot changes the original Aspire-inspired sketch] → intentional and stated
  plainly in proposal.md — showing real data honestly outranks matching Aspire's exact visual, and the change
  is easy to add later if/when a real per-destination health signal exists.

## Migration Plan

No migration — purely additive (new page, new mapping call in `Program.cs`, new docs). Rollback is deleting the
`Dashboard/` folder and the `Program.cs` call; nothing else depends on it.
