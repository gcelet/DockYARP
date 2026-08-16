## Why

`AdminApi:ApiKey` unset is documented as "the admin API is closed", which reads as "not active", but it is not:
`/api/*` and `/metrics` are always mapped regardless of `ApiKey` — "closed" only means the endpoint filter
unconditionally returns 401. Combined with `AdminApi:Host` also being unset by default (no `RequireHost`
constraint at all), **any DockYARP deployment with zero admin configuration can already shadow a backend's own
`/api/*` or `/metrics` route on every host it serves** — ASP.NET Core prioritizes the explicit admin route over
YARP's catch-all proxy route. There is no valid reason to intercept those paths when the operator never opted
into the admin surface at all. DockYARP needs a real, explicit switch.

## What Changes

- **BREAKING**: new `AdminApi:Surface` setting — an enum (`Disabled` [default], `Api`, `ApiAndDashboard`) that
  is the single, exhaustive control for what the admin surface exposes. It **replaces** both the implicit
  "`ApiKey` presence ⇒ closed/open" signal and the existing `AdminApi:DashboardEnabled` bool. A single
  three-state switch was chosen over two independent bools specifically so the illogical combination ("dashboard
  on, API off") cannot be represented in config at all — matching the project's own existing precedent for
  small closed state spaces (`TlsVersion`, `AddressFamilyPreference`) over coupled booleans.
- `Disabled` (default): `/api/*`, `/metrics`, and `/dashboard` are not mapped at all — requests fall through to
  normal reverse proxying, so a backend that owns one of those paths is never shadowed.
- `Api`: `/api/*` and `/metrics` respond as configured (behind `ApiKey`, unchanged); `/dashboard` is not served.
- `ApiAndDashboard`: both the JSON API and the dashboard are served — today's existing always-on behavior, now
  reachable only by explicit opt-in.
- **BREAKING**: any value other than `Disabled` with `AdminApi:Host` unset/empty now **fails fast at startup**
  (a plain configuration-validation exception, matching the project's existing `DataProtectionSetup`-style
  pattern — not `IOptions`/`IValidateOptions`, which the codebase doesn't use anywhere), instead of silently
  mapping the admin surface on every host. This replaces the "unset host = backward-compatible all-hosts"
  behavior the `admin-api` spec currently documents as intentional — that framing is no longer accurate once the
  surface is explicitly opt-in, and `AdminApi:Host` is also the dashboard's only trust boundary (no
  application-level auth), so requiring it whenever the surface is on is a real safety property.
- Documentation updated everywhere the admin API/base stack is shown (`examples.md`, `deployment.md`,
  `getting-started.md`, `migrating-from-nginx-proxy.md`, `configuration.md`, `features.md`) to add
  `AdminApi__Surface` (and a host, where the recipe already implies one) and explain the new safe-by-default
  behavior and the removal of `AdminApi:DashboardEnabled`.
- New/updated test coverage: `Disabled` (default) ⇒ nothing mapped, a backend's own `/api/*`/`/metrics` proxies
  normally on every host; non-`Disabled` + `Host` unset ⇒ startup fails; `Api` ⇒ API only, no dashboard;
  `ApiAndDashboard` ⇒ full dashboard (today's existing behavior, now behind the switch).

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `admin-api`: new requirement for the `AdminApi:Surface` switch (replacing the implicit `ApiKey`-presence
  signal and the `DashboardEnabled` bool); "Admin endpoint host isolation" requirement changes — `AdminApi:Host`
  becomes a hard startup requirement whenever `Surface` is not `Disabled`, replacing the current "unset host =
  backward-compatible all-hosts" behavior; "Read-only admin dashboard" requirement changes — the dashboard is
  now one of `Surface`'s three states rather than an independently-toggled bool.

## Impact

- `src/DockYarp.AdminApi/AdminApiSurface.cs` — new enum (`Disabled`, `Api`, `ApiAndDashboard`).
- `src/DockYarp.AdminApi/AdminApiOptions.cs` — new `Surface` property (default `Disabled`); **removes**
  `DashboardEnabled`.
- `src/DockYarp.App/Observability/AdminEndpointMapping.cs`, `src/DockYarp.Dashboard/DashboardServiceCollectionExtensions.cs`,
  `src/DockYarp.Dashboard/DashboardEndpointMapping.cs` — gate route mapping and dashboard service registration
  on `Surface`'s value instead of `DashboardEnabled`.
- Fail-fast guard for `Surface != Disabled` + empty `Host`, in
  `ObservabilityServiceCollectionExtensions.AddDockYarpObservability` right after binding `AdminApiOptions` — a
  plain `throw new InvalidOperationException(...)`.
- `tests/DockYarp.IntegrationTests/AdminApiIntegrationTests.cs` and the dashboard's own integration tests —
  new/updated coverage per the scenarios above; any test constructing `AdminApiOptions` with the old
  `DashboardEnabled` property updated to `Surface`.
- `docs-site/content/en/docs/{configuration,examples,deployment,getting-started,migrating-from-nginx-proxy,features}.md`
  — replace `AdminApi__ApiKey`-only / `AdminApi__DashboardEnabled` mentions with `AdminApi__Surface`, and
  document the new default.
