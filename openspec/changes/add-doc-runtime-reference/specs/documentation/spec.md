## ADDED Requirements

### Requirement: Runtime feature reference
The documentation site SHALL document DockYARP's runtime features — Docker discovery (health-aware exclusion,
network selection, container filters), routing and load balancing, automatic TLS/ACME behavior, observability
(the `/metrics` Prometheus endpoint and the structured access log with its field catalog), the admin API
endpoints (`/api/routes`, `/api/clusters`, `/api/certs`, `/api/health`, `/api/resolve`, protected by the API
key), file-based static configuration, custom error pages, and graceful shutdown — describing each feature's
behavior with a realistic example where applicable.

#### Scenario: Runtime features are documented
- **WHEN** a reader opens the runtime features reference
- **THEN** discovery, routing/load balancing, TLS, observability, the admin API, static configuration, error
  pages, and graceful shutdown are each described with their behavior

#### Scenario: The admin API endpoints are documented
- **WHEN** a reader consults the runtime features reference
- **THEN** the read-only endpoints (`/api/routes`, `/api/clusters`, `/api/certs`, `/api/health`) and
  `/api/resolve` are listed with the `X-Api-Key` requirement
