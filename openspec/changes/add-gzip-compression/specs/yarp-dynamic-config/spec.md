## ADDED Requirements

### Requirement: Response compression
The system SHALL compress responses (gzip and brotli) for compressible content types when the client indicates
support via `Accept-Encoding`. Compression SHALL be enabled by default and SHALL be disableable by
configuration. The system SHALL NOT compress a response that already carries a `Content-Encoding` (it does not
double-compress an upstream-encoded body).

#### Scenario: Compressible response is compressed
- **WHEN** compression is enabled and a client sends `Accept-Encoding: gzip` for a compressible text response
  with no upstream `Content-Encoding`
- **THEN** the response is returned with `Content-Encoding: gzip`

#### Scenario: Already-encoded response is not re-compressed
- **WHEN** a response already carries a `Content-Encoding`
- **THEN** the system forwards it without adding another compression layer

#### Scenario: Compression disabled by configuration
- **WHEN** compression is disabled by configuration
- **THEN** responses are returned without a `Content-Encoding` added by the proxy
