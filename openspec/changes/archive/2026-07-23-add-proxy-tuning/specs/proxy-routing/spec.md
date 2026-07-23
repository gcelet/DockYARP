## ADDED Requirements

### Requirement: Proxy tuning in the routing model
The routing model SHALL allow a cluster to carry an optional request timeout and a route to carry an
optional maximum request body size, so the proxy layer can apply per-backend timeouts and per-route upload
limits.

#### Scenario: Cluster carries a request timeout
- **WHEN** a cluster is created with a request timeout
- **THEN** the model exposes that timeout for the proxy layer

#### Scenario: Route carries a body-size limit
- **WHEN** a route is created with a maximum request body size
- **THEN** the model exposes that limit for the proxy layer
