## MODIFIED Requirements

### Requirement: Client certificate enforcement
The system SHALL reject a request to a route that requires a client certificate when the connection's
verification outcome is not successful — no certificate was presented, the certificate does not chain to the
configured CA, or the certificate is revoked per the configured CRL — responding with 403. Routes with a
requirement of `optional` or `none` SHALL NOT be rejected for a missing, untrusted, or revoked client
certificate.

#### Scenario: Required route without a client certificate is rejected
- **WHEN** a request targets a route requiring a client certificate and the connection presents none
- **THEN** the response status is 403 and the request is not proxied

#### Scenario: Required route with a revoked client certificate is rejected
- **WHEN** a request targets a route requiring a client certificate and the connection presents one whose serial
  number is listed in the configured CRL
- **THEN** the response status is 403 and the request is not proxied

#### Scenario: Required route with a client certificate is served
- **WHEN** a request targets a route requiring a client certificate and the connection presents one that chains
  to the configured CA and is not revoked
- **THEN** the request continues through the pipeline

#### Scenario: Optional route is served despite an untrusted or revoked client certificate
- **WHEN** a request targets a route with an `optional` client-certificate requirement and the connection
  presents a certificate that does not chain to the configured CA, or is revoked
- **THEN** the request continues through the pipeline (not rejected)

#### Scenario: Route without a requirement is served
- **WHEN** a request targets a route with no client-certificate requirement and presents none
- **THEN** the request continues through the pipeline
