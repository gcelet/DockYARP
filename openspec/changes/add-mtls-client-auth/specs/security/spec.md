## ADDED Requirements

### Requirement: Client certificate enforcement
The system SHALL reject a request to a route that requires a client certificate when no client certificate
was presented on the connection, responding with 403. Routes with a requirement of `optional` or `none`
SHALL NOT be rejected for a missing client certificate.

#### Scenario: Required route without a client certificate is rejected
- **WHEN** a request targets a route requiring a client certificate and the connection presents none
- **THEN** the response status is 403 and the request is not proxied

#### Scenario: Required route with a client certificate is served
- **WHEN** a request targets a route requiring a client certificate and the connection presents one
- **THEN** the request continues through the pipeline

#### Scenario: Route without a requirement is served
- **WHEN** a request targets a route with no client-certificate requirement and presents none
- **THEN** the request continues through the pipeline
