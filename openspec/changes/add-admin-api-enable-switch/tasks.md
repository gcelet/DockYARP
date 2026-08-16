## 1. Options and fail-fast validation (AG-AA)

- [x] 1.1 New `src/DockYarp.AdminApi/AdminApiSurface.cs`: enum `Disabled` (0), `Api`, `ApiAndDashboard`, with XML
      docs on each member.
- [x] 1.2 `src/DockYarp.AdminApi/AdminApiOptions.cs`: replace `DashboardEnabled` with
      `public AdminApiSurface Surface { get; set; }` (default `Disabled`), XML `<summary>`/`<remarks>` explaining
      the three states and that it replaces both the old implicit `ApiKey`-presence signal and
      `DashboardEnabled`.
- [x] 1.3 `src/DockYarp.App/Observability/ObservabilityServiceCollectionExtensions.cs`: right after
      `configuration.GetSection("AdminApi").Bind(adminApiOptions)`, add the fail-fast guard — `Surface` is not
      `Disabled` and `Host` is null/empty ⇒ `throw new InvalidOperationException(...)` naming both config keys
      and the remediation, matching `DataProtectionSetup.LoadEncryptionCertificate`'s message style.

## 2. Route mapping gates (AG-AA)

- [x] 2.1 `src/DockYarp.App/Observability/AdminEndpointMapping.cs` (`MapDockYarpAdmin`): return immediately
      (map nothing) when `options.Surface == AdminApiSurface.Disabled`.
- [x] 2.2 `src/DockYarp.Dashboard/DashboardServiceCollectionExtensions.cs` (`AddDockYarpDashboard`): register
      Razor Pages services only when `options.Surface == AdminApiSurface.ApiAndDashboard`.
- [x] 2.3 `src/DockYarp.Dashboard/DashboardEndpointMapping.cs` (`MapDockYarpDashboard`): return immediately
      unless `options.Surface == AdminApiSurface.ApiAndDashboard`.

## 3. Tests (AG-AA)

- [x] 3.1 Unit test: `AdminApiOptions.Surface` defaults to `AdminApiSurface.Disabled`
      (`AdminApiIntegrationTests.Surface_DefaultsToDisabled`).
- [x] 3.2 Integration test: `Surface = Disabled` (default) ⇒ `GET /api/health` and `GET /metrics` are not
      intercepted (`AdminApiIntegrationTests.SurfaceDisabled_DoesNotInterceptAdminPaths` — asserts not-401, the
      same weak-but-consistent style the existing host-isolation tests already use, no real backend needed).
- [x] 3.3 Integration test: non-`Disabled` `Surface` + empty `Host` ⇒ startup throws
      (`AdminApiIntegrationTests.SurfaceEnabled_WithoutHost_FailsFastAtStartup`, `[TestCase]`d for both `Api`
      and `ApiAndDashboard`).
- [x] 3.4 Integration test: `Surface = Api` ⇒ `/api/*` responds, `/dashboard` does not
      (`DashboardIntegrationTests.Dashboard_SurfaceApiOnly_IsNotServed`).
- [x] 3.5 Integration test: `Surface = ApiAndDashboard` ⇒ `/dashboard` responds
      (`DashboardIntegrationTests.Dashboard_ServesOnAdminHostWhenSurfaceIncludesDashboard` /
      `Dashboard_ServesOnTheAdminHost`).
- [x] 3.6 Updated every fixture that previously relied on the old always-on behavior (not just `DashboardEnabled`
      users — the implicit "`ApiKey` alone is enough" assumption broke far more fixtures than expected):
      `AdminApiIntegrationTests.cs`, `DashboardIntegrationTests.cs`, `AdminObservabilityTests.cs`, and
      `ResponseCompressionTests.cs` (its `/api/health` compression test was silently exercising the admin
      pipeline too). Full non-E2E suite green after the fix: 393/393 (`dotnet test` across all 6 non-E2E
      projects), including 93/93 in `DockYarp.IntegrationTests`.

## 4. Documentation sweep (AG-DOC)

- [x] 4.1 `configuration.md`: replaced the `DashboardEnabled` row with `Surface` (three values, default
      `Disabled`) in the `AdminApi` table, and reworded `Host`'s row to state the fail-fast requirement.
- [x] 4.2 **Adjusted from the original plan**: the base stack (`examples.md`) and the plain quick-starts
      (`getting-started.md`, `deployment.md`) can't set `Surface` alone — `Surface != Disabled` requires `Host`,
      and none of these generic recipes has a real host to put there. Instead: dropped the now-inert
      `AdminApi__ApiKey` line from all three, and added a one-line pointer ("admin API and dashboard are off by
      default... see Examples' dedicated admin host") so nothing in those recipes half-configures a surface that
      doesn't actually turn on.
- [x] 4.3 Done together with 4.2 (`deployment.md`, `getting-started.md`).
- [x] 4.4 `migrating-from-nginx-proxy.md`: same treatment — dropped `AdminApi__ApiKey` from both the basic and
      advanced worked examples (neither sets up a dedicated admin host; adding a placeholder admin domain would
      have added migration-guide scope this change didn't ask for) and added the same pointer note once.
- [x] 4.5 `features.md`: Admin API section opens with the `Surface` opt-in explanation before the endpoint table;
      dashboard section rewritten to describe the `Surface = ApiAndDashboard` requirement instead of
      `DashboardEnabled`.
- [x] Also updated `examples.md`'s existing `{{% alert %}}` shadowing warning (now explains the surface is safe
      by default, not just "how to fix the risk") and its "Dedicated admin host" recipe (`AdminApi__Surface:
      "Api"` added, comment on `Host` updated to state it's now required).
- [x] Also fixed the E2E harness (`tests/DockYarp.E2E.AppHost/Program.cs`): its `dockyarp` container used
      `/metrics` as an Aspire readiness health check with only `AdminApi__ApiKey` set — under the new default
      this would have silently broken the whole E2E gate (health check never succeeds ⇒ every E2E test times out
      waiting for the resource), not just admin-specific tests. Added `AdminApi__Surface: "Api"` +
      `AdminApi__Host: "localhost"` (bare, no port — `RequireHost` matches any port on that host, needed since
      Aspire assigns the published host port dynamically per run; verified via `microsoft-docs`).
- [x] Also fixed 4 test fixtures that broke under the new safe-by-default (found by running the full non-E2E
      suite, not assumed): `AdminObservabilityTests.cs` and `ResponseCompressionTests.cs` both exercised
      `/api/health` through a `WebApplicationFactory` that only set `AdminApi:ApiKey` — same fix
      (`Surface: "Api"`, `Host: "localhost"`). Confirmed green: 393/393 across all 6 non-E2E test projects
      (93/93 in `DockYarp.IntegrationTests`, the one most affected).

## 5. Spec sync prep (AG-AA)

- [x] 5.1 Verified the delta spec's ADDED requirement ("Admin surface enable switch") and MODIFIED requirements
      ("Admin endpoint host isolation", "Read-only admin dashboard") match what actually shipped in sections
      1-4, including the extra E2E/test-fixture/docs-outside-docs-site fixes found during implementation.
