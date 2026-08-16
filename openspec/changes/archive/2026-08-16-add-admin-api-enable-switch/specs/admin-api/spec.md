## ADDED Requirements

### Requirement: Admin surface enable switch
The system SHALL support an explicit `AdminApi:Surface` setting — an enum with exactly three values,
`Disabled` (default), `Api`, and `ApiAndDashboard` — as the single, exhaustive control for what the admin
surface exposes. When `Disabled`, `/api/*`, `/metrics`, and `/dashboard` SHALL NOT be intercepted — requests
fall through to normal reverse proxying, so a backend that happens to expose its own route at one of those
paths is never shadowed. When `Api`, the JSON admin API and `/metrics` SHALL respond as configured and
`/dashboard` SHALL NOT be served. When `ApiAndDashboard`, the JSON admin API, `/metrics`, and `/dashboard`
SHALL all respond as configured. No other combination (for example "dashboard without the API") SHALL be
representable.

#### Scenario: Disabled by default, no interception
- **WHEN** `AdminApi:Surface` is left at its default (`Disabled`)
- **THEN** `/api/*`, `/metrics`, and `/dashboard` are not mapped, and a backend proxied through DockYARP that
  owns a route at one of those paths is served normally on every host

#### Scenario: Enabling the API only
- **WHEN** `AdminApi:Surface` is `Api`
- **THEN** the JSON admin API and `/metrics` respond as configured, and `/dashboard` is not served

#### Scenario: Enabling the API and the dashboard
- **WHEN** `AdminApi:Surface` is `ApiAndDashboard`
- **THEN** the JSON admin API, `/metrics`, and `/dashboard` all respond as configured

## MODIFIED Requirements

### Requirement: Admin endpoint host isolation
The system SHALL support scoping the admin endpoints — the `/api/*` admin API and the `/metrics` endpoint — to a
dedicated host via the `AdminApi:Host` setting. When it is set, the admin endpoints SHALL respond only to requests
whose `Host` matches it; on every other host those paths SHALL fall through to normal reverse proxying, so a backend
that exposes an admin path (for example `/api/health` or `/metrics`) is not shadowed. **When `AdminApi:Surface` is
not `Disabled`, `AdminApi:Host` SHALL be required — the application SHALL fail to start (a configuration validation
error) if `Surface` is `Api` or `ApiAndDashboard` and `Host` is unset or empty.** This replaces the previous
unset-host "all-hosts" fallback: since the admin surface is now opt-in via `Surface`, an operator who opts in is
required to also scope it, rather than the surface silently defaulting to every host.

#### Scenario: Admin paths on other hosts are proxied, not shadowed
- **WHEN** `AdminApi:Host` is set and a request for a different host targets an admin path (e.g. `/api/health`)
- **THEN** the admin endpoint does not handle it, and the request is proxied (or falls to the default response) —
  not answered with the admin `401`

#### Scenario: Admin paths served on the admin host
- **WHEN** a request for the configured admin host targets an admin path
- **THEN** the admin endpoint handles it, behind the API-key protection

#### Scenario: Unset host keeps all-hosts behavior
- **WHEN** `AdminApi:Host` is unset and `AdminApi:Surface` is `Disabled` (its default)
- **THEN** the admin endpoints are not mapped at all — the "all hosts" question does not arise, since the
  surface itself is off (see "Admin surface enable switch")

#### Scenario: Enabling without a host fails fast at startup
- **WHEN** `AdminApi:Surface` is `Api` or `ApiAndDashboard` and `AdminApi:Host` is unset or empty
- **THEN** the application fails to start with a configuration validation error, rather than mapping the admin
  surface on every host

### Requirement: Read-only admin dashboard
The system SHALL provide a server-rendered, read-only HTML dashboard at `/dashboard`, scoped to `AdminApi:Host`
the same way as the admin API and `/metrics` (behaving as an admin endpoint for host-isolation purposes). The
dashboard SHALL render its data by reading the same underlying sources the admin API itself uses, without
making an HTTP request to `/api/*` from the browser, so no admin API key is ever present in the HTML or
JavaScript delivered to the client. The dashboard SHALL NOT carry application-level authentication; its
protection is `AdminApi:Host` not being internet-exposed — the same setting that a non-`Disabled`
`AdminApi:Surface` now requires to be set (see "Admin endpoint host isolation"). The dashboard SHALL ship with
no external CDN dependency and no JavaScript framework. **Whether the dashboard is served is one of
`AdminApi:Surface`'s three states (`ApiAndDashboard` serves it, `Api` and `Disabled` do not) — there is no
separate independent toggle for the dashboard.**

#### Scenario: Dashboard shows current resources and status
- **WHEN** an operator opens `/dashboard` on the admin host
- **THEN** they see the current routes and clusters, the certificate inventory with expiry, and the overall
  health/discovery status, refreshing without a manual page reload

#### Scenario: No admin API key reaches the browser
- **WHEN** the dashboard page is loaded, including on refresh
- **THEN** no admin API key or other credential is present anywhere in the delivered HTML or JavaScript

#### Scenario: Dashboard follows admin host isolation
- **WHEN** `AdminApi:Host` is set
- **THEN** `/dashboard` responds only on that host; on any other host it falls through to normal proxying, the
  same way `/api/*` already does

#### Scenario: No fabricated per-resource health
- **WHEN** the resources table lists a route or destination
- **THEN** it does not display a per-resource health indicator unless the underlying admin data actually
  carries one — the dashboard does not invent a signal that does not exist

#### Scenario: The dashboard can be disabled
- **WHEN** `AdminApi:Surface` is `Api` (the API without the dashboard)
- **THEN** `/dashboard` is not served (no route mapped, no dashboard-specific services registered), while the
  JSON admin API and `/metrics` continue to behave as configured
