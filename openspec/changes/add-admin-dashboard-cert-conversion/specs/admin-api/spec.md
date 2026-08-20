## MODIFIED Requirements

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
separate independent toggle for the dashboard. **The dashboard is read-only with exactly one narrow, explicitly
opt-in exception: certificate format conversion (see "Certificate format conversion from the dashboard") — no
other mutating action exists or is implied by this requirement.**

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

## ADDED Requirements

### Requirement: Certificate format conversion from the dashboard
The system SHALL support converting a stored certificate that is currently backed by a legacy `.pfx` file into
the canonical `{host}.crt`/`{host}.key` PEM pair, triggered from the admin dashboard, gated by an explicit
opt-in setting `AdminApi:AllowCertificateConversion` (default `false`). This is a **rewrite of the already-loaded
certificate's on-disk representation only** — it SHALL NOT re-provision, renew, or otherwise change which
certificate is served for that host. When `false`, no conversion action SHALL be available (neither rendered in
the UI nor honored if invoked directly). When `true`, the action SHALL be offered only for hosts currently
backed by a `.pfx` file — a host already on PEM SHALL NOT show a conversion action, since there is nothing to
convert. The conversion action SHALL be a state-changing (POST) operation protected by the framework's
anti-forgery mechanism, not a plain link — a GET-based mutating action would be forgeable by an unrelated page
loaded in the same browser session.

#### Scenario: Disabled by default, no action available
- **WHEN** `AdminApi:AllowCertificateConversion` is left at its default (`false`)
- **THEN** no conversion action is available for any host, whether or not it is `.pfx`-backed

#### Scenario: Converting a legacy PFX-backed certificate
- **WHEN** `AdminApi:AllowCertificateConversion` is `true` and an operator triggers conversion for a
  `.pfx`-backed host
- **THEN** that host's certificate is rewritten as `{host}.crt`/`{host}.key`, the stale `.pfx` file is removed,
  and the same certificate (same thumbprint) continues to be served for that host afterward

#### Scenario: No action offered for an already-PEM host
- **WHEN** a host's certificate is already backed by `{host}.crt`/`{host}.key`
- **THEN** no conversion action is offered for it, whether or not `AllowCertificateConversion` is `true`

#### Scenario: The conversion action is not exploitable via a forged link
- **WHEN** a request attempts to trigger the conversion action without a valid anti-forgery token (as a
  same-origin form submission from the dashboard page would carry)
- **THEN** the request is rejected, not honored
