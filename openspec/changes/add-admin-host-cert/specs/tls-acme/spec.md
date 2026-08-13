## ADDED Requirements

### Requirement: Reserved (non-route) certificate hosts
The system SHALL support provisioning certificates for **reserved** hosts that are not derived from routes — hosts the
proxy serves itself, such as the dedicated admin host — through a certificate-desire contribution point, without the
TLS layer depending on the admin subsystem. A reserved host SHALL be reconciled by the same provisioning/renewal loop
as route hosts (requested via ACME when missing, renewed before expiry, served via SNI once stored). Contribution
SHALL be **opt-in**: when no reserved host is contributed, provisioning behaves exactly as for routes alone. When a
reserved host duplicates a route host, it SHALL NOT cause a second provisioning of the same host.

#### Scenario: Reserved host is provisioned like a route host
- **WHEN** a reserved host is contributed (opted in) and has no current certificate
- **THEN** the provisioning service requests and stores a certificate for it, and renews it before expiry, exactly as
  it does for a host derived from a route

#### Scenario: No reserved host keeps route-only behavior
- **WHEN** no reserved host is contributed
- **THEN** the provisioning service reconciles exactly the route-derived hosts, unchanged

#### Scenario: Reserved host duplicating a route is not provisioned twice
- **WHEN** a reserved host equals a host already desired from a route
- **THEN** the merged desired set contains that host once (no duplicate provisioning)

### Requirement: Admin host certificate opt-in
The system SHALL let an operator opt the dedicated admin host (`AdminApi:Host`) into ACME certificate provisioning via
`AdminApi:LetsEncrypt`. When `AdminApi:LetsEncrypt` is enabled and `AdminApi:Host` is set, the admin host SHALL be
contributed as a reserved certificate host, using `AdminApi:ContactEmail` when provided and otherwise falling back to
`Tls:ContactEmail`. When the opt-in is disabled or the admin host is unset, no admin certificate SHALL be requested and
the admin host SHALL keep the default/operator-provided certificate.

#### Scenario: Opted-in admin host is contributed for provisioning
- **WHEN** `AdminApi:Host` is set and `AdminApi:LetsEncrypt` is `true`
- **THEN** the admin host is present in the reserved certificate hosts, with the resolved contact email

#### Scenario: Opt-in disabled requests no admin certificate
- **WHEN** `AdminApi:LetsEncrypt` is `false` (or `AdminApi:Host` is unset)
- **THEN** no admin host is contributed and the admin host keeps its default/operator certificate
