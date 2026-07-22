## ADDED Requirements

### Requirement: Options bound from configuration
The host SHALL bind its runtime options from configuration (appsettings and environment variables),
covering at least TLS/ACME (`AcmeDirectoryUri`, `AcceptTermsOfService`, contact email, certificate
directory, renewal margins), security headers, the Docker discovery endpoint, the admin API key, and the
shutdown timeout. Safe defaults SHALL apply when a value is absent (the ACME directory defaults to the
staging endpoint until explicitly overridden).

#### Scenario: Production ACME directory via configuration
- **WHEN** `Tls:AcmeDirectoryUri` and `Tls:AcceptTermsOfService` are set in configuration
- **THEN** the ACME client uses that directory and terms without any code change

#### Scenario: Security headers configurable
- **WHEN** security header options are provided in configuration
- **THEN** the security middleware emits headers according to those values

#### Scenario: Safe defaults when unset
- **WHEN** no ACME directory is configured
- **THEN** the ACME client uses the Let's Encrypt staging endpoint by default
