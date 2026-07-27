## ADDED Requirements

### Requirement: Strip the inbound Proxy header
The system SHALL remove a client-supplied `Proxy` request header before forwarding a request to a backend, to
mitigate the httpoxy vulnerability.

#### Scenario: Client-supplied Proxy header is not forwarded
- **WHEN** a client sends a request carrying a `Proxy` header
- **THEN** the backend receives the request without a `Proxy` header
