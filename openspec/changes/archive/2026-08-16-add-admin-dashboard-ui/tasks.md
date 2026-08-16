## 1. New `DockYarp.Dashboard` project (AG-AA)

- [x] 1.1 Create `src/DockYarp.Dashboard/DockYarp.Dashboard.csproj`: `Microsoft.NET.Sdk.Razor`,
      `<AddRazorSupportForMvc>true</AddRazorSupportForMvc>` (required for the `.cshtml` to register as a
      routable page — without it the SDK compiles the file but never wires the route; caught by a failing
      integration test, confirmed via the `RAZORSDK1004` build warning), `<FrameworkReference
      Include="Microsoft.AspNetCore.App" />`, project references to `DockYarp.AdminApi` + `DockYarp.Core`.
- [x] 1.2 Add the project to `DockYarp.slnx` (`/01-src/`) and as a `ProjectReference` from
      `DockYarp.App.csproj`.
- [x] 1.3 Add `AdminApiOptions.DashboardEnabled` (bool, default `true`) to `DockYarp.AdminApi`.

## 2. Dashboard page (AG-AA)

- [x] 2.1 Create `src/DockYarp.Dashboard/Pages/Dashboard/Index.cshtml` + `Index.cshtml.cs`, route pinned via
      `@page "/dashboard"`. The page model reads `IRouteConfigStore`, `ICertificateInventory`,
      `IDiscoveryHealth` via constructor injection and builds view data using `AdminMapper`/`AdminApiModels`.
      **Extracted while implementing**: the `/api/health` handler's inline status-resolution switch became
      `AdminMapper.ResolveHealth(IDiscoveryHealth)`, called from both `AdminEndpoints.cs` and the dashboard's
      page model — avoids duplicating that logic verbatim.
- [x] 2.2 Render: a status banner (Healthy/Degraded, discovery connected/disconnected, route/cluster/cert
      counts); a resources table (route host/path → cluster → destination addresses, no per-row health
      indicator — none exists); a certificates panel (host, `NotAfter`, a near-expiry flag computed at render
      time against `DateTimeOffset.UtcNow`, 14-day window).
- [x] 2.3 `<meta http-equiv="refresh" content="10">` for auto-refresh (no JavaScript).
- [x] 2.4 Inline `<style>` in the page (no `wwwroot`/`UseStaticFiles()` exists anywhere in the App today —
      adding a CSS file would mean wiring static-file serving for one file) — no external CDN, no framework;
      respects `prefers-color-scheme` for light/dark.

## 3. Registration and endpoint mapping (AG-AA)

- [x] 3.1 `DashboardServiceCollectionExtensions.AddDockYarpDashboard(this IServiceCollection, AdminApiOptions)`
      in `DockYarp.Dashboard`: calls `services.AddRazorPages()` only when `options.DashboardEnabled`.
- [x] 3.2 `DashboardEndpointMapping.MapDockYarpDashboard(this WebApplication)` in `DockYarp.Dashboard`: returns
      immediately when `!options.DashboardEnabled`; otherwise `app.MapRazorPages()`, `RequireHost`-scoped to
      `AdminApiOptions.Host` when set — mirrors `AdminEndpointMapping.MapDockYarpAdmin`'s shape, kept as its
      own method (not folded into that one) so an auth requirement can be layered on later.
- [x] 3.3 Wire into `DockYarp.App`: `ObservabilityServiceCollectionExtensions.AddDockYarpObservability` calls
      `services.AddDockYarpDashboard(adminApiOptions)` (reuses the `AdminApiOptions` it already binds — avoids
      a second bind and keeps `Program.cs`'s top-level statement count under the `AV1500` analyzer cap);
      `Program.cs` calls `app.MapDockYarpDashboard();` alongside the existing `app.MapDockYarpAdmin();`.

## 4. Tests (AG-AA)

- [x] 4.1 `DockYarp.IntegrationTests/DashboardIntegrationTests.cs`: mirrors `AdminApiIntegrationTests`'s
      host-isolation pattern — `/dashboard` responds on the admin host when `AdminApi:Host` is set, does not
      respond `200` on another host, and responds on all hosts when `AdminApi:Host` is unset.
- [x] 4.2 `Dashboard_ResponseDoesNotContainApiKey`: asserts the rendered body contains no substring matching
      the configured admin API key.
- [x] 4.3 `Dashboard_DisabledViaConfig_IsNotServed`: with `AdminApi:DashboardEnabled=false`, `/dashboard` does
      not respond `200`.

## 5. Docs (AG-AA / AG-DOC)

- [x] 5.1 `docs-site/content/en/docs/examples.md`: added `/dashboard` to the existing reserved-admin-paths
      warning (from `isolate-admin-api`), plus the no-auth/`DashboardEnabled` caveat.
- [x] 5.2 `docs-site/content/en/docs/features.md`: new "Admin dashboard" subsection under Admin API — describes
      `/dashboard`, states its auth model explicitly (network isolation only, no API key equivalent), and
      `AdminApi:DashboardEnabled`. Also added `DashboardEnabled` to `configuration.md`'s `AdminApi` table
      (pre-existing table this item extends).
- [x] 5.3 Checked `docs/labels-reference.md` — scoped strictly to container labels/env vars (`DockYarp.Docker`
      discovery); `AdminApi:*` application-config keys were never listed there (not even the pre-existing
      `ApiKey`/`Host`/`LetsEncrypt`/`ContactEmail`), confirming `DashboardEnabled` doesn't belong there either.
      No change.

## 6. Validation (AG-DEP)

- [x] 6.1 `./build.ps1 Test` green — 89/89 `DockYarp.IntegrationTests` (5 new dashboard tests), full solution
      unit + integration gate, warnings as errors (fixed 4 analyzer errors along the way: `MA0048` file/type
      name — justified suppression, framework convention; `AV1564` bool parameter — switched `CertificateRow`
      to a non-positional `init` record; `S3358` nested ternary in Razor markup — extracted to a
      `DiscoveryBadgeClass` computed property; `AV1500` `Program.cs` statement count — see task 3.3).
- [x] 6.2 `./build.ps1 Docs` — succeeded, no Hugo errors.
- [x] 6.3 Run `npx @fission-ai/openspec@latest validate add-admin-dashboard-ui --strict`.
