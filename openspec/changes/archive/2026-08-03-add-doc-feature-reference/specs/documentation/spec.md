## ADDED Requirements

### Requirement: Container configuration reference (labels and environment variables)
The documentation site SHALL provide a configuration reference that documents every container label and
environment variable DockYARP recognizes — the nginx-proxy-compatible keys (`VIRTUAL_*`, `LETSENCRYPT_*`,
`CERT_NAME`, `SSL_POLICY`, `HTTPS_METHOD`, `HSTS`, `NETWORK_ACCESS`, `SERVER_TOKENS`, `EXTERNAL_HTTPS_PORT`,
`ENABLE_HTTP_ON_MISSING_CERT`, `TRUST_DEFAULT_CERT`) and the `DOCKYARP_*` keys — with each entry's behavior,
default, and a realistic example using real key names. The reference SHALL state that any key may be set as a
label or as an environment variable, the environment variable taking precedence when both are set, and SHALL
note the recognized nginx-proxy namespaced label aliases.

#### Scenario: Every recognized key is documented
- **WHEN** a reader opens the configuration reference
- **THEN** every container label / environment variable DockYARP reads is listed with its behavior, default, and
  a realistic example (real key names, no placeholders)

#### Scenario: The label-or-environment channel is explained
- **WHEN** a reader consults the reference
- **THEN** it states that any key may be set as a label or an environment variable, with the environment
  variable winning when both are set for the same key
