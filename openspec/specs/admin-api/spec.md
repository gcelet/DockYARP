# admin-api Specification

## Purpose
TBD - created by archiving change add-admin-api. Update Purpose after archive.
## Requirements
### Requirement: Read-only admin endpoints
The system SHALL expose read-only endpoints `/api/routes`, `/api/clusters`, `/api/certs`, and
`/api/health` returning the current state as JSON, and SHALL NOT expose secrets such as Basic Auth
passwords.

#### Scenario: Routes endpoint returns active configuration
- **WHEN** an authorized client issues `GET /api/routes`
- **THEN** the response is the current active routing configuration as JSON

#### Scenario: Health endpoint reports status
- **WHEN** an authorized client issues `GET /api/health`
- **THEN** the response reports overall system health status

#### Scenario: Secrets are not exposed
- **WHEN** an authorized client reads `/api/routes` for a route protected by Basic Auth
- **THEN** the response indicates the route requires auth but does not include the password

### Requirement: Admin API protection
The system SHALL protect the admin API with an API key supplied in a request header, rejecting requests
with a missing or invalid key with 401.

#### Scenario: Unauthorized request is rejected
- **WHEN** a request to an admin endpoint lacks a valid API key
- **THEN** the response status is 401 and no state is returned

#### Scenario: Authorized request succeeds
- **WHEN** a request presents the configured API key
- **THEN** the endpoint returns its data

### Requirement: Observability
The system SHALL emit structured logs and expose a metrics endpoint (`/metrics`) in Prometheus format with
operational metrics such as the number of active routes and clusters.

#### Scenario: Metrics endpoint is scrapable
- **WHEN** a client issues `GET /metrics`
- **THEN** the response is a Prometheus exposition a scraper can parse

### Requirement: Certificate reporting
The `/api/certs` endpoint SHALL return the certificates currently in the certificate store (host and
expiry), without exposing private keys.

#### Scenario: Stored certificates are listed
- **WHEN** a certificate for `app.local` is in the store and an authorized client issues `GET /api/certs`
- **THEN** the response includes `app.local` with its expiry and no private key material

#### Scenario: Empty store returns an empty list
- **WHEN** no certificates are stored
- **THEN** `GET /api/certs` returns an empty list

### Requirement: Real health reporting
The `/api/health` endpoint SHALL report a status derived from real signals — Docker discovery connectivity
(when enabled), stored certificate count, and active route/cluster counts — and SHALL degrade the overall
status when a monitored dependency is unavailable.

#### Scenario: Healthy when dependencies are up
- **WHEN** discovery is connected (or disabled) and the store is populated
- **THEN** `GET /api/health` reports a healthy status with the real counts

#### Scenario: Degraded when a dependency is down
- **WHEN** Docker discovery is enabled but cannot reach the daemon
- **THEN** `GET /api/health` reports a degraded/unhealthy status

### Requirement: Access logging
The system SHALL emit a structured access-log entry for each handled request, including the request method,
host, path, response status, and elapsed time, unless access logging is disabled or the request path starts
with a configured excluded prefix (for infrastructure endpoints such as `/metrics` and `/api`). The rendered
format (text or JSON) follows the configured logging provider. The system SHALL support an operator-defined
field selection (`AccessLog:Fields`) from a fixed catalog; when configured, each entry SHALL contain exactly
those fields in the configured order, and when not configured the default fields SHALL be emitted unchanged.

#### Scenario: Request is logged
- **WHEN** access logging is enabled and a request is handled
- **THEN** a structured access-log entry with the method, path, response status, and elapsed time is emitted

#### Scenario: Logging can be disabled
- **WHEN** access logging is disabled
- **THEN** no access-log entry is emitted for a handled request

#### Scenario: Infrastructure paths are excluded
- **WHEN** a request targets a path under a configured excluded prefix (for example `/metrics`)
- **THEN** no access-log entry is emitted for it

#### Scenario: Custom field selection is honored
- **WHEN** `AccessLog:Fields` lists a specific set of fields
- **THEN** each access-log entry contains exactly those fields, in the configured order

#### Scenario: Default fields when unconfigured
- **WHEN** no `AccessLog:Fields` is configured
- **THEN** the default access-log fields are emitted unchanged

### Requirement: Configuration resolution endpoint
The admin API SHALL expose an authenticated endpoint that resolves a host and path to the effective
configuration, using the same route resolution as the request pipeline. The response SHALL include the matched
route, its transforms, TLS metadata, security policy, and target cluster, as sanitized JSON (no secrets). The
endpoint SHALL be protected by the admin API key like the other admin endpoints.

#### Scenario: Resolve returns the effective configuration
- **WHEN** an authenticated operator requests `/api/resolve` for a host and path that match a route
- **THEN** the response is JSON describing the matched route, transforms, TLS, security, and cluster

#### Scenario: No matching route
- **WHEN** an authenticated operator resolves a host/path that matches no route
- **THEN** the response indicates no match (404)

#### Scenario: Unauthenticated resolve is rejected
- **WHEN** the resolve endpoint is requested without a valid API key
- **THEN** the request is rejected with 401

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

### Requirement: Admin endpoint host isolation
The system SHALL support scoping the admin endpoints — the `/api/*` admin API and the `/metrics` endpoint — to a
dedicated host via the `AdminApi:Host` setting. When it is set, the admin endpoints SHALL respond only to requests
whose `Host` matches it; on every other host those paths SHALL fall through to normal reverse proxying, so a backend
that exposes an admin path (for example `/api/health` or `/metrics`) is not shadowed. When `AdminApi:Surface` is
not `Disabled`, `AdminApi:Host` SHALL be required — the application SHALL fail to start (a configuration validation
error) if `Surface` is `Api` or `ApiAndDashboard` and `Host` is unset or empty. Since the admin surface is opt-in
via `Surface`, an operator who opts in is required to also scope it, rather than the surface silently defaulting
to every host.

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
no external CDN dependency and no JavaScript framework. Whether the dashboard is served is one of
`AdminApi:Surface`'s three states (`ApiAndDashboard` serves it, `Api` and `Disabled` do not) — there is no
separate independent toggle for the dashboard.

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

### Requirement: Certificate download from the dashboard
The system SHALL support downloading a stored certificate's public material (`{host}.crt`, the leaf plus any
chain) and its private key (`{host}.key`) from the admin dashboard, gated by an explicit opt-in setting
`AdminApi:AllowCertificateDownload` (default `false`). When `false`, no download route SHALL be mapped and no
download link SHALL be rendered on `/dashboard` — the same "not mapped when disabled" pattern the dashboard
itself already follows for `AdminApi:Surface`. When `true`, the download routes SHALL be reachable only under
the same host-isolation boundary as the rest of the dashboard (`AdminApi:Host`), not routed through the
API-key-protected `/api/*` surface — a browser-initiated download SHALL NOT require the admin API key to reach
the browser, preserving the existing "no admin API key in the delivered HTML/JavaScript" guarantee. Requesting
a download for a host with no stored certificate SHALL return 404, not an error page or empty file.

#### Scenario: Disabled by default, nothing exposed
- **WHEN** `AdminApi:AllowCertificateDownload` is left at its default (`false`)
- **THEN** no certificate download route responds, and `/dashboard`'s certificate table shows no download link

#### Scenario: Downloading the public certificate
- **WHEN** `AdminApi:AllowCertificateDownload` is `true` and an operator downloads a stored certificate for a
  known host
- **THEN** they receive `{host}.crt` as a PEM file attachment containing the leaf and any chain certificates

#### Scenario: Downloading the private key
- **WHEN** `AdminApi:AllowCertificateDownload` is `true` and an operator downloads the private key for a known
  host
- **THEN** they receive `{host}.key` as a PEM file attachment

#### Scenario: Download follows the dashboard's host isolation
- **WHEN** `AdminApi:AllowCertificateDownload` is `true` and `AdminApi:Host` is set
- **THEN** the download routes respond only on the admin host, the same way `/dashboard` itself does

#### Scenario: Download never requires the admin API key in the browser
- **WHEN** an operator downloads a certificate or private key from the dashboard
- **THEN** the request succeeds without any admin API key being present in the page, a cookie, or a header the
  browser had to be given

#### Scenario: Unknown host returns 404
- **WHEN** a download is requested for a host with no stored certificate
- **THEN** the response is 404, not a server error or an empty/malformed file
