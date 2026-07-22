## ADDED Requirements

### Requirement: TLS protocol and cipher hardening
The system SHALL apply configurable TLS hardening to the HTTPS endpoint: a minimum TLS version (default TLS
1.2), the enabled HTTP protocols (default HTTP/1.1 and HTTP/2), and an optional cipher-suite allow-list. The
minimum version SHALL map to the corresponding enabled TLS protocols; the cipher allow-list SHALL be applied
only on platforms that support explicit cipher selection and otherwise ignored.

#### Scenario: Minimum TLS version maps to enabled protocols
- **WHEN** the minimum TLS version is configured as TLS 1.2
- **THEN** the HTTPS endpoint enables TLS 1.2 and TLS 1.3

#### Scenario: Minimum TLS 1.3 excludes older protocols
- **WHEN** the minimum TLS version is configured as TLS 1.3
- **THEN** the HTTPS endpoint enables only TLS 1.3

### Requirement: HTTPS-only hosts are not provisioned
The system SHALL exclude a host whose HTTPS method is `nohttps` from certificate provisioning, since it is
not served over HTTPS.

#### Scenario: nohttps host is skipped
- **WHEN** a route declares a certificate host but sets the HTTPS method to `nohttps`
- **THEN** that host is not included in the set of desired certificates
