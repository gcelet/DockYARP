## MODIFIED Requirements

### Requirement: TLS protocol and cipher hardening
The system SHALL apply configurable TLS hardening to the HTTPS endpoint: a minimum TLS version (default TLS
1.2), the enabled HTTP protocols (default HTTP/1.1 and HTTP/2), and an optional cipher-suite allow-list. The
minimum version SHALL map to the corresponding enabled TLS protocols; the cipher allow-list SHALL be applied
only on platforms that support explicit cipher selection and otherwise ignored. The system SHALL also accept a
named `Tls:SslPolicy` preset (Mozilla `Modern`, `Intermediate`, `Old`) that sets the minimum TLS version and a
default cipher-suite list; an explicit cipher-suite allow-list SHALL override the preset's ciphers, and an
unrecognized or unset policy SHALL leave the configured values unchanged.

#### Scenario: Minimum TLS version maps to enabled protocols
- **WHEN** the minimum TLS version is configured as TLS 1.2
- **THEN** the HTTPS endpoint enables TLS 1.2 and TLS 1.3

#### Scenario: Minimum TLS 1.3 excludes older protocols
- **WHEN** the minimum TLS version is configured as TLS 1.3
- **THEN** the HTTPS endpoint enables only TLS 1.3

#### Scenario: Modern preset selects TLS 1.3
- **WHEN** `Tls:SslPolicy` is `Mozilla-Modern`
- **THEN** the effective minimum version is TLS 1.3 and the cipher list is the TLS 1.3 suites

#### Scenario: Explicit ciphers override the preset
- **WHEN** `Tls:SslPolicy` names a preset and `Tls:CipherSuites` is also set
- **THEN** the explicit cipher list is used instead of the preset's ciphers

#### Scenario: Unknown policy falls back
- **WHEN** `Tls:SslPolicy` is unset or unrecognized
- **THEN** the configured minimum version and cipher allow-list are used unchanged
