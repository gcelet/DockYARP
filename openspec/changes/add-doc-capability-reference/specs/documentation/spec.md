## ADDED Requirements

### Requirement: Application configuration reference
The documentation site SHALL document the proxy's own application configuration — each configuration section
(`Server`, `Docker`, `Tls`, `Security`, `Routing`, `Proxy`, `AccessLog`, `AdminApi`, `Compression`,
`DataProtection`, `Host`) with its keys, their defaults, and their purpose — and SHALL state that any key may be
set via `appsettings.json` or a double-underscore environment variable (for example `Tls__AcceptTermsOfService`).

#### Scenario: Each application-configuration section is documented with defaults
- **WHEN** a reader opens the Configuration page's application-configuration reference
- **THEN** every section is listed with its keys, each key's default, and its purpose

#### Scenario: The appsettings-or-environment channel is explained
- **WHEN** a reader consults the application-configuration reference
- **THEN** it states that any key may be set via `appsettings.json` or a `Section__Key` environment variable
