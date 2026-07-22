## ADDED Requirements

### Requirement: HSTS preload and per-host override
The system SHALL support an HSTS `preload` directive on the global policy and a per-host HSTS override
carried on the route's TLS metadata: a per-host value replaces the emitted `Strict-Transport-Security`
header, and `off` suppresses HSTS for that host. HSTS is only emitted on HTTPS responses.

#### Scenario: Preload directive is emitted
- **WHEN** HSTS preload is enabled and an HTTPS response is produced
- **THEN** the `Strict-Transport-Security` header includes `preload`

#### Scenario: Per-host override suppresses HSTS
- **WHEN** an HTTPS response is produced for a host whose route sets HSTS to `off`
- **THEN** no `Strict-Transport-Security` header is emitted for that response

### Requirement: Reject HTTPS for a nohttps host
The system SHALL refuse an HTTPS request whose matched route selects the `nohttps` method, since that host
is served over HTTP only.

#### Scenario: HTTPS request to a nohttps host is refused
- **WHEN** an HTTPS request targets a host whose route selects `nohttps`
- **THEN** the response status is 404 and the request is not proxied
