## ADDED Requirements

### Requirement: Read-only admin dashboard
The system SHALL provide a server-rendered, read-only HTML dashboard at `/dashboard`, scoped to `AdminApi:Host`
the same way as the admin API and `/metrics` (behaving as an admin endpoint for host-isolation purposes). The
dashboard SHALL render its data by reading the same underlying sources the admin API itself uses, without
making an HTTP request to `/api/*` from the browser, so no admin API key is ever present in the HTML or
JavaScript delivered to the client. The dashboard SHALL NOT carry application-level authentication; its
protection is the operator ensuring `AdminApi:Host` is not internet-exposed. The dashboard SHALL ship with no
external CDN dependency and no JavaScript framework. The dashboard SHALL be disableable via configuration,
independently of the JSON admin API.

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
- **WHEN** `AdminApi:DashboardEnabled` is set to `false`
- **THEN** `/dashboard` is not served (no route mapped, no dashboard-specific services registered), while the
  JSON admin API and `/metrics` continue to behave as configured
