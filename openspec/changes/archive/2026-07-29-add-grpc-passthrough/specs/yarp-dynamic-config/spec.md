## ADDED Requirements

### Requirement: gRPC backend protocol
The system SHALL support declaring a backend as gRPC via `VIRTUAL_PROTO=grpc` (plaintext/HTTP-2) or
`VIRTUAL_PROTO=grpcs` (TLS/HTTP-2). For such a backend the system SHALL contact the cluster over HTTP/2 exactly
(no version downgrade), so gRPC calls — including trailers — are proxied. `grpc` SHALL use the http scheme and
`grpcs` the https scheme for the backend address.

#### Scenario: gRPC backend uses HTTP/2
- **WHEN** a backend declares `VIRTUAL_PROTO=grpc`
- **THEN** the cluster contacts the backend over HTTP/2 (exact version) using the http scheme

#### Scenario: gRPCs backend uses TLS and HTTP/2
- **WHEN** a backend declares `VIRTUAL_PROTO=grpcs`
- **THEN** the cluster contacts the backend over HTTP/2 (exact version) using the https scheme

#### Scenario: gRPC is a recognized protocol
- **WHEN** `VIRTUAL_PROTO` is `grpc` or `grpcs`
- **THEN** it is accepted as a valid protocol (not reported as unsupported)
