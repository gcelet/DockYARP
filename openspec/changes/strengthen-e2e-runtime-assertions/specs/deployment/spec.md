## ADDED Requirements

### Requirement: End-to-end runtime security assertions
The end-to-end suite SHALL additionally assert, against the real runtime, security behaviors that cannot be
observed in-process: that a proxied response does not expose a `Server` header, and that an HTTP→HTTPS
redirect uses status 308. These assertions SHALL be integrated into existing scenarios that already exercise
the corresponding flow, not a synthetic combined test.

#### Scenario: Proxied response omits the Server header
- **WHEN** a request is proxied to a discovered backend over the real runtime
- **THEN** the response carries no `Server` header

#### Scenario: HTTP→HTTPS redirect uses 308
- **WHEN** an HTTP request is sent for a certificate-backed host whose HTTPS method is redirect
- **THEN** the response status is 308 and the `Location` is the HTTPS URL for the same host and path
